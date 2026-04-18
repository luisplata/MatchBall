using System;
using Domain.Entities;
using Infra.WebAdapter;

namespace Infra.WebAdapter.Mapper
{
    public static class HeroMapper
    {
        public static Hero ToDomain(HeroDto dto)
        {
            return new Hero(
                dto.id,
                dto.heroId ?? string.Empty,
                dto.name ?? string.Empty,
                dto.metadata?.role ?? string.Empty,
                ParseDate(dto.created_at),
                ParseDate(dto.updated_at)
            );
        }

        private static DateTime ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.MinValue;

            return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }
    }
}


