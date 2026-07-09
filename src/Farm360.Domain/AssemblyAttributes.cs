// Farm360.Domain — Assembly-level attributes
// Allow Farm360.Persistence to access internal Domain methods
// (e.g., AuditableEntity.SetCreatedAudit / SetModifiedAudit called by AuditSaveChangesInterceptor).
// This is the only approved cross-assembly internal access in the system.
// Constitution §2: Dependencies flow inward; Persistence → Domain is the approved direction.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Farm360.Persistence")]
[assembly: InternalsVisibleTo("Farm360.Domain.UnitTests")]
