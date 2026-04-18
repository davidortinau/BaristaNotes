using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BaristaNotes.Core.Data;

public class BaristaNotesContextFactory : IDesignTimeDbContextFactory<BaristaNotesContext>
{
    public BaristaNotesContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BaristaNotesContext>();
        optionsBuilder.UseSqlite("Data Source=barista_notes.db");
        
        return new BaristaNotesContext(optionsBuilder.Options);
    }
}
