using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TelegramMultiBot.Database.Models;

namespace ConfigUI.Pages.Hosts
{
    public class EditModel : PageModel
    {
        private readonly TelegramMultiBot.Database.BoberDbContext _context;

        public EditModel(TelegramMultiBot.Database.BoberDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public TelegramMultiBot.Database.Models.Host Host { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(string address, int port)
        {
            if (address == null || port == 0)
            {
                return NotFound();
            }

            var host =  await _context.Hosts.FindAsync(address, port);
            if (host == null)
            {
                return NotFound();
            }
            Host = host;
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(Host).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!HostExists(Host.Address, Host.Port))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("../Index");
        }

        private bool HostExists(string address, int port)
        {
            return _context.Hosts.Any(e => e.Address == address && e.Port == port);
        }
    }
}
