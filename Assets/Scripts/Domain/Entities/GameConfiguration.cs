namespace Domain.Entities
{
    public record GameConfiguration(int exp_by_lose, int exp_by_win, int powerup_ice_per_buy, int powerup_joker_per_buy, int powerup_lead_per_buy, int powerup_ray_per_buy, int how_much_match_to_get_power_up);
}