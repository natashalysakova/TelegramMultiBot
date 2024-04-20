using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Models;

namespace ConfigUI.Pages.Hosts
{
    public class DeleteModel : PageModel
    {
        private readonly TelegramMultiBot.Database.BoberDbContext _context;

        public DeleteModel(TelegramMultiBot.Database.BoberDbContext context)
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

            var host = await _context.Hosts.FindAsync(address, port);

            if (host == null)
            {
                return NotFound();
            }
            else
            {
                Host = host;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string address, int port)
        {
            if (address == null || port == 0)
            {
                return NotFound();
            }

            var host = await _context.Hosts.FindAsync(address, port);
            if (host != null)
            {
                Host = host;
                _context.Hosts.Remove(Host);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("../Index");
        }
    }
}
