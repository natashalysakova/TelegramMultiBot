using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Models;

namespace ConfigUI.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly BoberDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public IndexModel(ILogger<IndexModel> logger, BoberDbContext dbContext, IConfiguration configuration)
        {
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        public IEnumerable<Model> Models { get; set; }
        public IEnumerable<TelegramMultiBot.Database.Models.Host> Hosts { get; set; }

        [BindProperty]
        public List<TelegramMultiBot.Database.Models.Settings> Settings { get; set; }

        public void OnGet()
        {
            Models = _dbContext.Models.AsNoTracking();
            Hosts = _dbContext.Hosts.AsNoTracking();
            Settings = _dbContext.Settings.AsNoTracking().ToList();
        }

        public async Task<IActionResult> OnPostUpdateSettings()
        {
            foreach (var model in Settings)
            {
                var entry = _dbContext.Settings.Find(model.SettingSection, model.SettingsKey);
                if(entry != null)
                    entry.SettingsValue = model.SettingsValue;
            }
            _dbContext.SaveChanges();
            return StatusCode(200);
        }

        public IActionResult OnPostUpdateHostState(string address, int port, bool enabled)
        {
            var host = _dbContext.Hosts.Find(address, port);
            if (host == null)
            {
                return NotFound();
            }

            host.Enabled = enabled;
            _dbContext.SaveChangesAsync();

            return StatusCode(200);
        }
    }
}
