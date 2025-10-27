using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ConfigUI.Pages.Hosts
{
    public class CreateModel : PageModel
    {
        private readonly TelegramMultiBot.Database.BoberDbContext _context;

        public CreateModel(TelegramMultiBot.Database.BoberDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public TelegramMultiBot.Database.Models.Host Host { get; set; } = default!;

        // To protect from overposting attacks, see https://aka.ms/RazorPagesCRUD
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Hosts.Add(Host);
            await _context.SaveChangesAsync();

            return RedirectToPage("../Index");
        }
    }
}
