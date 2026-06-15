using System.Collections.ObjectModel;

namespace SaveInvestigator;

public static class ManagedReconNotesFactsExtractor
{
    public static ManagedReconNotesFacts Extract()
    {
        var typeNotes = new List<ManagedReconTypeNoteFact>
        {
            new(
                "Game.Prefabs.PrefabRef",
                "Game",
                "Value-type ECS component with field Unity.Entities.Entity m_Prefab.",
                "Test whether PrefabRef adds a distinct save-backed building identity dimension.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Buildings.Building",
                "Game",
                "Value-type ECS component with road-edge, curve-position, option-mask, and building-flags fields.",
                "Test whether building-side structure separates base identity from child or upgrade identity in the save.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Buildings.TransportStation",
                "Game",
                "Value-type ECS component with operational fields like comfort/loading factors, refuel energy types, and TransportStationFlags.",
                "Test whether transport station structure sharpens unresolved transport building ceilings in the save.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Buildings.PublicTransportStation",
                "Game",
                "Empty/tag-style ECS component with no direct payload fields.",
                "Test whether public transport station structure distinguishes resolved stations from unresolved transport buildings in the save.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Companies.CompanyData",
                "Game",
                "Value-type ECS component with thin payload: Random seed plus Brand entity.",
                "Test whether company data structure suggests a save probe that improves company semantics.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Economy.Resources",
                "Game",
                "Buffer-element ECS type with Game.Economy.Resource m_Resource and System.Int32 m_Amount.",
                "Test whether resources structure exposes a save-backed path to company resource-state interpretation.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Companies.ResourceBuyer",
                "Game",
                "Value-type ECS component with payer entity, SetupTargetFlags, resource-needed, amount-needed, and location fields.",
                "Test whether resource buyer structure exposes unmet or ongoing company demand in the save.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Companies.ResourceSeller",
                "Game",
                "Empty/tag-style ECS component with no direct payload fields.",
                "Test whether resource seller structure exposes persisted selling or trade-state clues in the save.",
                "unvalidated",
                "managed_only"),
            new(
                "Game.Companies.TradeCost",
                "Game",
                "Buffer-element ECS type with resource, buy-cost, sell-cost, and last-transfer-request-time fields.",
                "Test whether trade cost structure suggests a real save-backed business-state probe.",
                "unvalidated",
                "managed_only")
        };

        return new ManagedReconNotesFacts(new ReadOnlyCollection<ManagedReconTypeNoteFact>(typeNotes));
    }
}
