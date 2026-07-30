using FYP.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace FYP.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Register all database tables
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<FraudAlert> FraudAlerts { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SavedBankCard> SavedBankCards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configure ChatMessage Relationships (Dual Foreign Keys to User Table)
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderID)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading deletes to preserve forensic data

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverID)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. Enforce Cryptographic Uniqueness Constraints
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique(); // Mathematically blocks accidental double-charges at the database level

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // 3. Configure Precise Decimal Precisions for Financial Integrity
            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Refund>()
                .Property(r => r.RefundAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<FraudAlert>()
                .Property(fa => fa.RiskScore)
                .HasPrecision(5, 2);
            
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique(); 

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict); 
        }
    }
}