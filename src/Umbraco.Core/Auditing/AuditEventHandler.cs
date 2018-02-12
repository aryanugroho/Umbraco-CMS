﻿using System;
using System.Linq;
using System.Threading;
using System.Web;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Security;
using Umbraco.Core.Services;

namespace Umbraco.Core.Auditing
{
    internal class AuditEventHandler : ApplicationEventHandler
    {
        private IAuditService _auditServiceInstance;
        private IUserService _userServiceInstance;
        private IEntityService _entityServiceInstance;

        private IUser PerformingUser
        {
            get
            {
                var identity = Thread.CurrentPrincipal?.GetUmbracoIdentity();
                return identity == null
                    ? new User { Id = 0, Name = "SYSTEM", Email = "" }
                    : _userServiceInstance.GetUserById(Convert.ToInt32(identity.Id));
            }
        }

        private string PerformingIp
        {
            get
            {
                var httpContext = HttpContext.Current == null ? (HttpContextBase) null : new HttpContextWrapper(HttpContext.Current);
                var ip = httpContext.GetCurrentRequestIpAddress();
                if (ip.ToLowerInvariant().StartsWith("unknown")) ip = "";
                return ip;
            }
        }

        protected override void ApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            _auditServiceInstance = applicationContext.Services.AuditService;
            _userServiceInstance = applicationContext.Services.UserService;
            _entityServiceInstance = applicationContext.Services.EntityService;

            //BackOfficeUserManager.AccountLocked += ;
            //BackOfficeUserManager.AccountUnlocked += ;
            BackOfficeUserManager.ForgotPasswordRequested += OnForgotPasswordRequest;
            BackOfficeUserManager.ForgotPasswordChangedSuccess += OnForgotPasswordChange;
            BackOfficeUserManager.LoginFailed += OnLoginFailed;
            //BackOfficeUserManager.LoginRequiresVerification += ;
            BackOfficeUserManager.LoginSuccess += OnLoginSuccess;
            BackOfficeUserManager.LogoutSuccess += OnLogoutSuccess;
            BackOfficeUserManager.PasswordChanged += OnPasswordChanged;
            BackOfficeUserManager.PasswordReset += OnPasswordReset;
            //BackOfficeUserManager.ResetAccessFailedCount += ;

            UserService.SavedUserGroup += OnSavedUserGroup;

            UserService.SavedUser += OnSavedUser;
            UserService.DeletedUser += OnDeletedUser;
            UserService.UserGroupPermissionsAssigned += UserGroupPermissionAssigned;

            MemberService.Saved += OnSavedMember;
            MemberService.Deleted += OnDeletedMember;
            MemberService.AssignedRoles += OnAssignedRoles;
            MemberService.RemovedRoles += OnRemovedRoles;
        }

        private string FormatEmail(IMember member)
        {
            return member == null ? string.Empty : member.Email.IsNullOrWhiteSpace() ? "" : $"<{member.Email}>";
        }

        private string FormatEmail(IUser user)
        {
            return user == null ? string.Empty : user.Email.IsNullOrWhiteSpace() ? "" : $"<{user.Email}>";
        }

        private void OnRemovedRoles(IMemberService sender, RolesEventArgs args)
        {
            var performingUser = PerformingUser;
            var roles = string.Join(", ", args.Roles);
            var members = sender.GetAllMembers(args.MemberIds).ToDictionary(x => x.Id, x => x);
            foreach (var id in args.MemberIds)
            {
                members.TryGetValue(id, out var member);
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    -1, $"Member {id} \"{member?.Name ?? "(unknown)"}\" {FormatEmail(member)}",
                    "umbraco/member/roles/removed", $"roles modified, removed {roles}");
            }
        }

        private void OnAssignedRoles(IMemberService sender, RolesEventArgs args)
        {
            var performingUser = PerformingUser;
            var roles = string.Join(", ", args.Roles);
            var members = sender.GetAllMembers(args.MemberIds).ToDictionary(x => x.Id, x => x);
            foreach (var id in args.MemberIds)
            {
                members.TryGetValue(id, out var member);
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    -1, $"Member {id} \"{member?.Name ?? "(unknown)"}\" {FormatEmail(member)}",
                    "umbraco/member/roles/assigned", $"roles modified, assigned {roles}");
            }
        }

        private void OnSavedUserGroup(IUserService sender, SaveEventArgs<IUserGroup> saveEventArgs)
        {
            var performingUser = PerformingUser;
            var groups = saveEventArgs.SavedEntities;
            foreach (var group in groups)
            {
                var dp = string.Join(", ", ((UserGroup) group).GetPreviouslyDirtyProperties());
                var sections = ((UserGroup)group).WasPropertyDirty("AllowedSections")
                    ? string.Join(", ", group.AllowedSections)
                    : null;
                var perms = ((UserGroup)group).WasPropertyDirty("Permissions")
                    ? string.Join(", ", group.Permissions)
                    : null;

                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    -1, $"User Group {group.Id} \"{group.Name}\" ({group.Alias})",
                    "umbraco/user-group/save", $"updating {(string.IsNullOrWhiteSpace(dp) ? "(nothing)" : dp)};{(sections == null ? "" : $", assigned sections: {sections}")}{(perms == null ? "" : $", assigned perms: {perms}")}");
            }
        }

        private void UserGroupPermissionAssigned(IUserService sender, SaveEventArgs<EntityPermission> saveEventArgs)
        {
            var performingUser = PerformingUser;
            var perms = saveEventArgs.SavedEntities;
            foreach (var perm in perms)
            {
                var group = sender.GetUserGroupById(perm.UserGroupId);
                var assigned = string.Join(", ", perm.AssignedPermissions);
                var entity = _entityServiceInstance.Get(perm.EntityId);

                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    -1, $"User Group {group.Id} \"{group.Name}\" ({group.Alias})",
                    "umbraco/user-group/permissions-change", $"assigning {(string.IsNullOrWhiteSpace(assigned) ? "(nothing)" : assigned)} on id:{perm.EntityId} \"{entity.Name}\"");
            }
        }

        private void OnSavedMember(IMemberService sender, SaveEventArgs<IMember> saveEventArgs)
        {
            var performingUser = PerformingUser;
            var members = saveEventArgs.SavedEntities;
            foreach (var member in members)
            {
                var dp = string.Join(", ", ((Member) member).GetPreviouslyDirtyProperties());

                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    -1, $"Member {member.Id} \"{member.Name}\" {FormatEmail(member)}",
                    "umbraco/member/save", $"updating {(string.IsNullOrWhiteSpace(dp) ? "(nothing)" : dp)}");
            }
        }

        private void OnDeletedMember(IMemberService sender, DeleteEventArgs<IMember> deleteEventArgs)
        {
            var performingUser = PerformingUser;
            var members = deleteEventArgs.DeletedEntities;
            foreach (var member in members)
            {
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    -1, $"Member {member.Id} \"{member.Name}\" {FormatEmail(member)}",
                    "umbraco/member/delete", $"delete member id:{member.Id} \"{member.Name}\" {FormatEmail(member)}");
            }
        }

        private void OnSavedUser(IUserService sender, SaveEventArgs<IUser> saveEventArgs)
        {
            var performingUser = PerformingUser;
            var affectedUsers = saveEventArgs.SavedEntities;
            foreach (var affectedUser in affectedUsers)
            {
                var groups = affectedUser.WasPropertyDirty("Groups")
                    ? string.Join(", ", affectedUser.Groups.Select(x => x.Alias))
                    : null;

                var dp = string.Join(", ", ((User)affectedUser).GetPreviouslyDirtyProperties());

                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    affectedUser.Id, $"User \"{affectedUser.Name}\" {FormatEmail(affectedUser)}",
                    "umbraco/user/save", $"updating {(string.IsNullOrWhiteSpace(dp) ? "(nothing)" : dp)}{(groups == null ? "" : "; groups assigned: " + groups)}");
            }
        }

        private void OnDeletedUser(IUserService sender, DeleteEventArgs<IUser> deleteEventArgs)
        {
            var performingUser = PerformingUser;
            var affectedUsers = deleteEventArgs.DeletedEntities;
            foreach (var affectedUser in affectedUsers)
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", PerformingIp,
                    DateTime.Now,
                    affectedUser.Id, $"User \"{affectedUser.Name}\" {FormatEmail(affectedUser)}",
                    "umbraco/user/delete", "delete user");
        }

        private void OnLoginSuccess(object sender, EventArgs args)
        {
            if (args is IdentityAuditEventArgs identityArgs)
            {
                var performingUser = _userServiceInstance.GetUserById(identityArgs.PerformingUser);
                if (performingUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.PerformingUser}");
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", identityArgs.IpAddress,
                    DateTime.Now,
                    0, null,
                    "umbraco/user/sign-in/login", "login success");
            }
        }

        private void OnLogoutSuccess(object sender, EventArgs args)
        {
            if (args is IdentityAuditEventArgs identityArgs)
            {
                var performingUser = _userServiceInstance.GetUserById(identityArgs.PerformingUser);
                if (performingUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.PerformingUser}");
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", identityArgs.IpAddress,
                    DateTime.Now,
                    0, null,
                    "umbraco/user/sign-in/logout", "logout success");
            }
        }

        private void OnPasswordReset(object sender, EventArgs args)
        {
            if (args is IdentityAuditEventArgs identityArgs)
            {
                if (identityArgs.PerformingUser < 0) return;
                var performingUser = _userServiceInstance.GetUserById(identityArgs.PerformingUser);
                if (performingUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.PerformingUser}");
                var affectedUser = _userServiceInstance.GetUserById(identityArgs.AffectedUser);
                if (affectedUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.AffectedUser}");
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", identityArgs.IpAddress,
                    DateTime.Now,
                    affectedUser.Id, $"User \"{affectedUser.Name}\" {FormatEmail(affectedUser)}",
                    "umbraco/user/password/reset", "password reset");
            }
        }

        private void OnPasswordChanged(object sender, EventArgs args)
        {
            if (args is IdentityAuditEventArgs identityArgs)
            {
                if (identityArgs.PerformingUser < 0) return;
                var performingUser = _userServiceInstance.GetUserById(identityArgs.PerformingUser);
                if (performingUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.PerformingUser}");
                var affectedUser = _userServiceInstance.GetUserById(identityArgs.AffectedUser);
                if (affectedUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.AffectedUser}");
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", identityArgs.IpAddress,
                    DateTime.Now,
                    affectedUser.Id, $"User \"{affectedUser.Name}\" {FormatEmail(affectedUser)}",
                    "umbraco/user/password/change", "password change");
            }
        }

        private void OnLoginFailed(object sender, EventArgs args)
        {
            if (args is IdentityAuditEventArgs identityArgs)
            {
                if (identityArgs.PerformingUser < 0) return;
                var performingUser = _userServiceInstance.GetUserById(identityArgs.PerformingUser);
                if (performingUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.PerformingUser}");
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", identityArgs.IpAddress,
                    DateTime.Now,
                    0, null,
                    "umbraco/user/sign-in/failed", "login failed");
            }
        }

        private void OnForgotPasswordChange(object sender, EventArgs args)
        {
            if (args is IdentityAuditEventArgs identityArgs)
            {
                var performingUser = _userServiceInstance.GetUserById(identityArgs.PerformingUser);
                if (performingUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.PerformingUser}");
                var affectedUser = _userServiceInstance.GetUserById(identityArgs.AffectedUser);
                if (affectedUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.AffectedUser}");
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", identityArgs.IpAddress,
                    DateTime.Now,
                    affectedUser.Id, $"User \"{affectedUser.Name}\" {FormatEmail(affectedUser)}",
                    "umbraco/user/password/forgot/change", "password forgot/change");
            }
        }

        private void OnForgotPasswordRequest(object sender, EventArgs args)
        {
            if (args is IdentityAuditEventArgs identityArgs)
            {
                if (identityArgs.PerformingUser < 0) return;
                var performingUser = _userServiceInstance.GetUserById(identityArgs.PerformingUser);
                if (performingUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.PerformingUser}");
                var affectedUser = _userServiceInstance.GetUserById(identityArgs.AffectedUser);
                if (affectedUser == null) throw new InvalidOperationException($"No user found with id {identityArgs.AffectedUser}");
                _auditServiceInstance.Write(performingUser.Id, $"User \"{performingUser.Name}\" {FormatEmail(performingUser)}", identityArgs.IpAddress,
                    DateTime.Now,
                    affectedUser.Id, $"User \"{affectedUser.Name}\" {FormatEmail(affectedUser)}",
                    "umbraco/user/password/forgot/request", "password forgot/request");
            }
        }
    }
}
