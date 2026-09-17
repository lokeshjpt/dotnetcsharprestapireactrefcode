namespace PWA.PermitsApi.Application.DTOs;

public sealed record HazardDto
{
    public string? ConsultantFirstName { get; init; }
    public string? ConsultantLastName { get; init; }
    public string? ConsultantPhone { get; init; }
    public string? ConsultantCell { get; init; }

    public string? SafetyOfficerFirstName { get; init; }
    public string? SafetyOfficerLastName { get; init; }
    public string? SafetyOfficerPhone { get; init; }
    public string? SafetyOfficerCell { get; init; }

    public string? FacilityType { get; init; }
    public DateTime? SiteSafetyMeetingDateTime { get; init; }
    public DateTime? AddTs { get; init; }

    public string? PpeLevelA { get; init; }
    public string? PpeLevelB { get; init; }
    public string? PpeLevelC { get; init; }
    public string? PpeLevelD { get; init; }

    public string? EquipHardHatFlag { get; init; }
    public string? EquipSafetyShoesFlag { get; init; }
    public string? EquipOrangeVestFlag { get; init; }
    public string? EquipHearingProtFlag { get; init; }
    public string? EquipSafetyEyewearFlag { get; init; }
    public string? EquipClothingFlag { get; init; }
    public string? EquipClothingDesc { get; init; }
    public string? EquipRespiratorFlag { get; init; }
    public string? EquipRespiratorDesc { get; init; }
    public string? EquipCartridgeFlag { get; init; }
    public string? EquipCartridgeDesc { get; init; }
    public string? EquipGlovesFlag { get; init; }
    public string? EquipGlovesDesc { get; init; }
    public string? EquipOtherFlag { get; init; }
    public string? EquipOtherDesc { get; init; }

    public string? InfoProvidedByCompanyName { get; init; }
    public string? InfoProvidedByLastName { get; init; }
    public string? InfoProvidedByFirstName { get; init; }
    public string? InfoProvidedByTitle { get; init; }
    public string? InfoProvidedByPhone { get; init; }
    public string? Acknowledgement { get; init; }

    public IReadOnlyList<string> Contaminants { get; init; } = new List<string>();
    public IReadOnlyList<HazardSubstanceDto> Substances { get; init; } = new List<HazardSubstanceDto>();
}

public sealed record HazardSubstanceDto(string? Concentration, string? PelPpm, string? HealthEffects);

public sealed record DocumentLinkDto(int SeqNum, string? DocumentType, string? OtherTypeDesc, string? FileName);
