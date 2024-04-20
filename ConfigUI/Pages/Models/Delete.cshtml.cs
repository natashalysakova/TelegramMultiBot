using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Models;

namespace ConfigUI.Pages.Models
{
    public class DeleteModel : PageModel
    {
        private readonly TelegramMultiBot.Database.BoberDbContext _context;

        public DeleteModel(TelegramMultiBot.Database.BoberDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Model Model { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Models.FirstOrDefaultAsync(m => m.Name == id);

            if (model == null)
            {
                return NotFound();
            }
            else
            {
                Model = model;
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var model = await _context.Models.FindAsync(id);
            if (model != null)
            {
                Model = model;
                _context.Models.Remove(Model);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("../Index");
        }
    }
}
