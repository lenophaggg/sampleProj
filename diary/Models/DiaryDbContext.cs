using diary.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace diary.Models
{
    public class DiaryDbContext : DbContext
    {
        public DiaryDbContext(DbContextOptions<DiaryDbContext> options)
            : base(options)
        {
        }
        public DbSet<PersonContactUserData> PersonContactUsers { get; set; }
        public DbSet<StudentData> Students { get; set; }
        public DbSet<GroupHeadData> GroupHeads { get; set; }
        public DbSet<ClassData> Classes { get; set; }
        public DbSet<AttendanceData> Attendance { get; set; }
        public DbSet<StudentAbsencesData> StudentAbsences { get; set; }

        public DbSet<ClassGroupAssignmentData> ClassGroupAssignments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AttendanceData>()
                .Property(a => a.Date)
                .HasColumnType("date"); 

            base.OnModelCreating(modelBuilder);

        }
    }
}
