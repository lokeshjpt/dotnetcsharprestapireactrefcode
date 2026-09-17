namespace PWA.PermitsApi.Domain.Models;

public sealed class HazardInfo
{
    public string AppId { get; set; } = string.Empty;

    // ONSITE_CONSULTANT_* columns
    public string? ConsultantFirstName { get; set; }
    public string? ConsultantLastName { get; set; }
    public string? ConsultantPhone { get; set; }
    public string? ConsultantCell { get; set; }

    // SAFETY_OFFICER_* columns
    public string? SafetyOfficerFirstName { get; set; }
    public string? SafetyOfficerLastName { get; set; }
    public string? SafetyOfficerPhone { get; set; }
    public string? SafetyOfficerCell { get; set; }

    public string? FacilityType { get; set; }
    public DateTime? SiteSafetyMeetingDateTime { get; set; }
    public DateTime? AddTs { get; set; }

    // PERSONAL_PROTECTION_EQUIP_LEVEL_A..D (Y/N)
    public string? PpeLevelA { get; set; }
    public string? PpeLevelB { get; set; }
    public string? PpeLevelC { get; set; }
    public string? PpeLevelD { get; set; }

    // EQUIP_*_FLAG (R = required, A = available/on-site) and matching EQUIP_*_DESC
    public string? EquipHardHatFlag { get; set; }
    public string? EquipSafetyShoesFlag { get; set; }
    public string? EquipOrangeVestFlag { get; set; }
    public string? EquipHearingProtFlag { get; set; }
    public string? EquipSafetyEyewearFlag { get; set; }
    public string? EquipClothingFlag { get; set; }
    public string? EquipClothingDesc { get; set; }
    public string? EquipRespiratorFlag { get; set; }
    public string? EquipRespiratorDesc { get; set; }
    public string? EquipCartridgeFlag { get; set; }
    public string? EquipCartridgeDesc { get; set; }
    public string? EquipGlovesFlag { get; set; }
    public string? EquipGlovesDesc { get; set; }
    public string? EquipOtherFlag { get; set; }
    public string? EquipOtherDesc { get; set; }

    // INFO_PROVIDED_BY_* and ACKNOWLEDGEMENT
    public string? InfoProvidedByCompanyName { get; set; }
    public string? InfoProvidedByLastName { get; set; }
    public string? InfoProvidedByFirstName { get; set; }
    public string? InfoProvidedByTitle { get; set; }
    public string? InfoProvidedByPhone { get; set; }
    public string? Acknowledgement { get; set; }

    // APP_SITE_CONTAMINATIONS.CONTAMINATION (0..n)
    public IList<string> Contaminations { get; set; } = new List<string>();

    // APP_HAZARD_SUBSTANCE rows (0..n)
    public IList<HazardSubstance> Substances { get; set; } = new List<HazardSubstance>();
}
