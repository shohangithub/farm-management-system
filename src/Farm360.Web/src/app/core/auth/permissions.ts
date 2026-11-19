/**
 * Permission codes, mirroring Farm360.Persistence/Seed/PermissionConstants.cs.
 * The backend is the authority (every route checks these); the UI uses them only to hide
 * screens and actions the user cannot perform.
 */
export const Permissions = {
  Finance: {
    View: 'finance.view',
    Create: 'finance.create',
    Edit: 'finance.edit',
    Delete: 'finance.delete',
    Admin: 'finance.admin',
  },
  Dashboard: { View: 'dashboard.view' },
  Analytics: { View: 'analytics.view' },
  Intelligence: { View: 'intelligence.view' },
  Billing: { View: 'billing.view', Manage: 'billing.manage' },
  Reports: { View: 'reports.view' },
  Users: {
    View: 'users.view',
    Invite: 'users.invite',
    Edit: 'users.edit',
    Remove: 'users.remove',
    AssignRole: 'users.assignrole',
  },
} as const;
