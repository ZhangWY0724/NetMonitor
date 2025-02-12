using Microsoft.EntityFrameworkCore;

namespace IISState.Models;

public class MonitorContext:DbContext
{
    public MonitorContext(DbContextOptions<MonitorContext> options) : base(options) { }


    public DbSet<MonitorItem> MonitorItem { get; set; }
    
    public DbSet<HistoryEntry> HistoryEntry { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 确保正确配置模型
        modelBuilder.Entity<MonitorItem>()
            .HasMany(m => m.History)
            .WithOne(h =>h.MonitorItem)
            .HasForeignKey(h => h.MonitorItemId)
            .OnDelete(DeleteBehavior.Cascade);
    } 
}