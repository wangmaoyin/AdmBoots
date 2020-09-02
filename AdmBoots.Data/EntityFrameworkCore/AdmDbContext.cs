﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdmBoots.Domain.Models;
using AdmBoots.Infrastructure.CustomExceptions;
using AdmBoots.Infrastructure.Framework.Web;
using Microsoft.EntityFrameworkCore;

namespace AdmBoots.Data.EntityFrameworkCore {

    public class AdmDbContext : DbContext {
        private readonly IAdmSession _admSession;

        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Menu> Menus { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<UserRole> UserRoles { get; set; }
        public virtual DbSet<RoleMenu> RoleMenus { get; set; }

        public AdmDbContext(DbContextOptions<AdmDbContext> options, IAdmSession admSession)
         : base(options) {
            _admSession = admSession;
        }

        public override int SaveChanges() {
            try {
                ApplyAdmConcepts();
                var result = base.SaveChanges();
                return result;
            } catch (DbUpdateConcurrencyException ex) {
                throw new AdmDbConcurrencyException(ex.Message, ex);
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
            try {
                ApplyAdmConcepts();
                var result = await base.SaveChangesAsync(cancellationToken);
                return result;
            } catch (DbUpdateConcurrencyException ex) {
                throw new AdmDbConcurrencyException(ex.Message, ex);
            }
        }

        private void ApplyAdmConcepts() {
            var userId = _admSession.UserId;
            var userName = _admSession.UserName;
            foreach (var entry in ChangeTracker.Entries().ToList()) {
                switch (entry.State) {
                    case EntityState.Added:
                        AdmEntityAuditingHelper.SetCreationAuditProperties(entry.Entity, userId, userName);
                        break;

                    case EntityState.Modified:
                        AdmEntityAuditingHelper.SetModificationAuditProperties(entry.Entity, userId, userName);
                        break;
                        //case EntityState.Deleted:
                        //    SetDeletionAuditProperties(entry.Entity);
                        //break;
                }
            }
        }
    }
}
