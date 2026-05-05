using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using TelegramMultiBot.Database.Interfaces;

namespace VideoDownloader.Client;

public class GalleryDlClient
{
    private readonly ISqlConfiguationService _sqlConfigurationService;
    private readonly ILogger<GalleryDlClient> _logger;
    private const string DockerSocketUri = "unix:///var/run/docker.sock";

    public GalleryDlClient(ISqlConfiguationService sqlConfigurationService, ILogger<GalleryDlClient> logger)
    {
        _sqlConfigurationService = sqlConfigurationService;
        _logger = logger;
    }

    private const long ExitCodeSuccess = 0;
    // gallery-dl exits with 1 when at least one download succeeded but others failed (partial success)
    private const long ExitCodePartialSuccess = 1;

    /// <summary>
    /// Downloads photos for the given URL by running a gallery-dl Docker container on demand.
    /// Returns the paths of the downloaded files accessible from the bot container.
    /// </summary>
    public async Task<IReadOnlyList<string>> DownloadAsync(string url, string jobId, CancellationToken cancellationToken)
    {
        if (!IsValidJobId(jobId))
            throw new ArgumentException($"Invalid jobId: '{jobId}'. Must be a valid GUID.", nameof(jobId));

        var settings = _sqlConfigurationService.VideoDownloaderSettings;
        var downloadsBasePath = settings.GalleryDlDownloadsPath;
        var volumeName = settings.GalleryDlVolumeName;
        var image = settings.GalleryDlImage;
        var outputDir = Path.Combine(downloadsBasePath, jobId);

        // Do not pre-create the output directory — gallery-dl creates it when it writes files.
        // The bot container runs as a non-root user and cannot create directories in a
        // root-owned named volume.

        _logger.LogInformation("Running gallery-dl for {url} into {outputDir}", url, outputDir);

        using var dockerClient = CreateDockerClient();

        string containerId = string.Empty;
        try
        {
            var containerResponse = await dockerClient.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Image = image,
                    Cmd = ["--dest", $"/output/{jobId}/", url],
                    HostConfig = new HostConfig
                    {
                        // Use a named-volume mount so both the bot container and the
                        // gallery-dl container share the same Docker volume. Using a
                        // host-path bind (Binds with a container-side path) would
                        // resolve against the host filesystem, not the bot container.
                        Mounts =
                        [
                            new Mount
                            {
                                Type = "volume",
                                Source = volumeName,
                                Target = "/output"
                            }
                        ],
                        AutoRemove = false
                    }
                }, cancellationToken);

            containerId = containerResponse.ID;
            _logger.LogDebug("Created gallery-dl container {containerId}", containerId);

            await dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), cancellationToken);

            var waitResponse = await dockerClient.Containers.WaitContainerAsync(containerId, cancellationToken);
            _logger.LogInformation("gallery-dl container exited with code {exitCode} for {url}", waitResponse.StatusCode, url);

            if (waitResponse.StatusCode != ExitCodeSuccess && waitResponse.StatusCode != ExitCodePartialSuccess)
            {
                var logs = await GetContainerLogsAsync(dockerClient, containerId);
                _logger.LogWarning("gallery-dl exited with code {code} for {url}. Logs: {logs}", waitResponse.StatusCode, url, logs);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(containerId))
            {
                try
                {
                    await dockerClient.Containers.RemoveContainerAsync(containerId,
                        new ContainerRemoveParameters { Force = true }, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove gallery-dl container {containerId}", containerId);
                }
            }
        }

        var files = Directory.Exists(outputDir)
            ? Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)
                .Where(IsImageFile)
                .OrderBy(f => f)
                .ToList()
            : [];

        _logger.LogInformation("gallery-dl downloaded {count} image(s) for {url}", files.Count, url);
        return files;
    }

    public void CleanupJobDirectory(string jobId)
    {
        var settings = _sqlConfigurationService.VideoDownloaderSettings;
        var dir = Path.Combine(settings.GalleryDlDownloadsPath, jobId);
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
                _logger.LogDebug("Deleted gallery-dl directory {dir}", dir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete gallery-dl directory {dir}", dir);
        }
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp";
    }

    private static bool IsValidJobId(string jobId)
    {
        return Guid.TryParse(jobId, out _);
    }

    private static DockerClient CreateDockerClient()
    {
        return new DockerClientConfiguration(new Uri(DockerSocketUri)).CreateClient();
    }

    /// <summary>
    /// Pulls the gallery-dl Docker image. Intended to be called once at application startup
    /// so that per-job execution never requires a pull.
    /// </summary>
    public async Task PullImageAsync(CancellationToken cancellationToken)
    {
        var image = _sqlConfigurationService.VideoDownloaderSettings.GalleryDlImage;

        // Split image name and tag correctly, accounting for registry URLs that may contain
        // a port number (e.g. registry.example.com:5000/image:tag). The tag is the portion
        // after the last colon only if that portion contains no '/' (which would indicate a
        // registry host:port, not a tag).
        string imageName;
        string tag;
        var lastColon = image.LastIndexOf(':');
        if (lastColon >= 0 && !image[(lastColon + 1)..].Contains('/'))
        {
            imageName = image[..lastColon];
            tag = image[(lastColon + 1)..];
        }
        else
        {
            imageName = image;
            tag = "latest";
        }

        _logger.LogInformation("Pulling gallery-dl image {image}", image);
        using var dockerClient = CreateDockerClient();
        try
        {
            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = imageName, Tag = tag },
                null,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Status))
                        _logger.LogTrace("Pull {image}: {status}", image, msg.Status);
                }),
                cancellationToken);
            _logger.LogInformation("gallery-dl image {image} is ready", image);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not pull gallery-dl image {image}; jobs will use locally cached image if available", image);
        }
    }

    private static async Task<string> GetContainerLogsAsync(DockerClient dockerClient, string containerId)
    {
        try
        {
            using var logStream = await dockerClient.Containers.GetContainerLogsAsync(containerId,
                tty: false,
                new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Tail = "50" });

            var (stdout, stderr) = await logStream.ReadOutputToEndAsync(CancellationToken.None);
            return (stdout + "\n" + stderr).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }
}
