using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace DemoRecorder.Utils
{
    public static class GameRules
    {
        public static CCSGameRules? _gameRules;

        private static CCSGameRules? GetGameRule()
        {
            _gameRules ??= Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                    .FirstOrDefault(static e => e != null && e.IsValid)?.GameRules;
            return _gameRules;
        }

        public static object? Get(string rule)
        {
            _ = GetGameRule();
            System.Reflection.PropertyInfo? property = _gameRules?.GetType().GetProperty(rule);
            return property?.CanRead == true ? property.GetValue(_gameRules) : null;
        }
    }
}