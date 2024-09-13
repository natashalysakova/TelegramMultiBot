param ([Parameter(Mandatory)][string]$versionTag, $buildBot=$True, $buildConfig=$true)
function BuildAndPush {
    param (
        [string]$image,
        [string]$dockerFile,
        [string]$tag
    )
    
    docker build --platform=linux/arm64 -t ghcr.io/natashalysakova/$image`:latest . -f $dockerFile
    #docker build -t ghcr.io/natashalysakova/$image`:latest . -f $dockerFile
    docker image tag ghcr.io/natashalysakova/$image`:latest ghcr.io/natashalysakova/$image`:$tag
    docker push ghcr.io/natashalysakova/$image`:latest
    docker push ghcr.io/natashalysakova/$image`:$tag
}

if ($buildBot) {
    BuildAndPush -image "bober-bot" -dockerFile ".\TelegramMultiBot\Dockerfile" -tag $versionTag
}

if ($buildConfig){
    BuildAndPush -image "bober-config-ui" -dockerFile ".\ConfigUI\Dockerfile" -tag $versionTag
}