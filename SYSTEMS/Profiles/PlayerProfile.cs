// Stores the stable identity and display name of a local commander.
using System;

namespace MassiveRts;

public sealed record PlayerProfile(string Id, string Name, DateTime CreatedUtc);
