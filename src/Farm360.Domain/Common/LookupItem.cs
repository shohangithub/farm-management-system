using System;

namespace Farm360.Domain.Common;

/// <summary>
/// A generic, lightweight projection used across the application for dropdowns and selectors.
/// </summary>
public record LookupItem(Guid Id, string Name, Guid? ParentId = null);
