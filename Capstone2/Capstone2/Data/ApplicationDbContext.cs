using Capstone2.Models;
using Microsoft.EntityFrameworkCore;

namespace Capstone2.Data
{
    public class ApplicationDbContext : DbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<HeadWaiter>()
                .HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.Restrict); //  prevent cascade here

            modelBuilder.Entity<Waiter>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade); //  or restrict too if needed

            modelBuilder.Entity<Order>()
                .HasOne(o => o.HeadWaiter)
                .WithMany()
                .HasForeignKey(o => o.HeadWaiterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Each PurchaseOrder optionally has one ViewTransaction
            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.ViewTransaction)
                .WithMany()
                .HasForeignKey(po => po.ViewTransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Prevent cascading deletes from Supplier to dependents to avoid multiple cascade paths
            modelBuilder.Entity<PurchaseOrder>()
                .HasOne(po => po.Supplier)
                .WithMany()
                .HasForeignKey(po => po.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ViewTransaction>()
                .HasOne(vt => vt.Supplier)
                .WithMany()
                .HasForeignKey(vt => vt.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Each SupplierTransaction optionally has one ViewTransaction
            modelBuilder.Entity<SupplierTransaction>()
                .HasOne(st => st.ViewTransaction)
                .WithMany()
                .HasForeignKey(st => st.ViewTransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Prevent cascading deletes from Supplier to SupplierTransaction
            modelBuilder.Entity<SupplierTransaction>()
                .HasOne(st => st.Supplier)
                .WithMany()
                .HasForeignKey(st => st.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<SupplierTransaction> SupplierTransactions { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<ViewTransaction> ViewTransactions { get; set; }


        public DbSet<Order> Orders { get; set; }
        public DbSet<HeadWaiter> HeadWaiters { get; set; }
        public DbSet<OrderWaiter> OrderWaiters { get; set; }
        public DbSet<Waiter> Waiters { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<MaterialPullOut> MaterialPullOuts { get; set; }
        public DbSet<MaterialPullOutItem> MaterialPullOutItems { get; set; }
        public DbSet<MaterialReturn> MaterialReturns { get; set; }

        public DbSet<Capstone2.Models.Menu> Menu { get; set; } = default!;
        public DbSet<Capstone2.Models.MenuPackages> MenuPackages { get; set; } = default!;
        public DbSet<AuditLog> AuditLogs { get; set; }
    }

}
