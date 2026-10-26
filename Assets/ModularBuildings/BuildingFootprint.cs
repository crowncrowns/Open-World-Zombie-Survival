namespace ZombieSurvival.ModularBuildings
{
    /// <summary>New V6 single-entrance bodies add two complete 3 m facade bays.</summary>
    public static class BuildingFootprint
    {
        public static float SectionWidth(BuildingSeriesProfile profile,int entrances)
            =>profile.extendedCityRules&&entrances==1?21f:profile.section.sectionWidth;
        public static float SectionWidth(GeneratedBuilding building)
            =>building.generatedSectionWidth>0?building.generatedSectionWidth:building.profile.section.sectionWidth;
    }
}
