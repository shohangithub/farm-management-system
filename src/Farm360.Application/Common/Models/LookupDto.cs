using System;

namespace Farm360.Application.Common.Models;

public record LookupDto(Guid Id, string Name, Guid? ParentId = null);
