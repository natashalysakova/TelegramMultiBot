using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TelegramMultiBot.Database.Models;

namespace ConfigUI.Pages.Models;

public class EditModel : PageModel
{
    private readonly TelegramMultiBot.Database.BoberDbContext _context;

    public EditModel(TelegramMultiBot.Database.BoberDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Model Model { get; set; } = default!;
    public IEnumerable<SelectListItem> Versions { get; set; } = Utility.GetEnumAsSelectItemList<TelegramMultiBot.Database.Enums.ModelVersion>();

    

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var model =  await _context.Models.FirstOrDefaultAsync(m => m.Name == id);
        if (model == null)
        {
            return NotFound();
        }
        Model = model;
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

        _context.Attach(Model).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ModelExists(Model.Name))
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

    private bool ModelExists(string id)
    {
        return _context.Models.Any(e => e.Name == id);
    }
}
