namespace AiSeasonCreator.Helpers
{
    public class NameAndAsset
    {
        /// <summary>Stable identifier (season id, car id or schedule index) — names are not unique.</summary>
        public int Id { get; set; }
        public string Name { get; set; } = "";
        /// <summary>Optional extra text shown on hover (dates, config name...).</summary>
        public string? Tooltip { get; set; }
        public Image? Asset { get; set; }
    }
}
