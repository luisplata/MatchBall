namespace App.Reward
{
    /// <summary>
    /// Display metadata for one reward row. The icon key is an addressable key; resolving it to a
    /// sprite stays in the presentation layer, so this type stays free of engine dependencies.
    /// </summary>
    public sealed record RewardDisplayData(string IconKey, string Label);
}
