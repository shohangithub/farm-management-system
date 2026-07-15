// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "CQRS Handlers are instantiated by MediatR via reflection.", Scope = "namespaceanddescendants", Target = "~N:Farm360.Application.Health")]
