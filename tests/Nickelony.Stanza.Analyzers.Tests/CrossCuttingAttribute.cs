using System;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>
/// Marks a test that drives a rule for another behavior, or runs fixes it does not pin.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class CrossCuttingAttribute : Attribute;
