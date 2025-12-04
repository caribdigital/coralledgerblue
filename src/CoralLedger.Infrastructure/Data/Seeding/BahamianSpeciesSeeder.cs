using CoralLedger.Domain.Entities;
using CoralLedger.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Infrastructure.Data.Seeding;

/// <summary>
/// Seeds the Bahamian species database with priority species from Dr. Bethel's watchlist
/// Includes invasive species (Lionfish) and threatened/endangered species
/// </summary>
public static class BahamianSpeciesSeeder
{
    public static async Task SeedAsync(MarineDbContext context)
    {
        if (await context.BahamianSpecies.AnyAsync())
            return;

        var species = GetBahamianSpecies().ToList();
        await context.BahamianSpecies.AddRangeAsync(species);
        await context.SaveChangesAsync();
    }

    private static IEnumerable<BahamianSpecies> GetBahamianSpecies()
    {
        // INVASIVE SPECIES - High Priority
        yield return BahamianSpecies.Create(
            scientificName: "Pterois volitans",
            commonName: "Red Lionfish",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.LeastConcern,
            localName: "Lionfish",
            isInvasive: true,
            description: "Highly invasive Indo-Pacific lionfish decimating native reef fish populations. Voracious predator consuming over 50 native species.",
            identificationTips: "Venomous spines, bold red/white banding, large fan-like pectoral fins. 18 venomous dorsal spines.",
            habitat: "Coral reefs, artificial structures, mangroves",
            typicalDepthMinM: 1,
            typicalDepthMaxM: 300);

        yield return BahamianSpecies.Create(
            scientificName: "Pterois miles",
            commonName: "Devil Firefish",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.LeastConcern,
            localName: "Devil Lionfish",
            isInvasive: true,
            description: "Second invasive lionfish species in Bahamas waters. Similar impact to Red Lionfish.",
            identificationTips: "Similar to Red Lionfish but with fewer dorsal fin rays (10 vs 11).",
            habitat: "Coral reefs, rocky substrates",
            typicalDepthMinM: 2,
            typicalDepthMaxM: 85);

        // CRITICALLY ENDANGERED CORALS
        yield return BahamianSpecies.Create(
            scientificName: "Acropora palmata",
            commonName: "Elkhorn Coral",
            category: SpeciesCategory.Coral,
            conservationStatus: ConservationStatus.CriticallyEndangered,
            localName: "Elkhorn",
            description: "Critical reef-building coral. Population declined 80-98% since 1980s. Extremely vulnerable to bleaching, disease, and storm damage.",
            identificationTips: "Large flat branches resembling elk antlers. Golden-brown to yellow-brown color. Branches up to 2m long.",
            habitat: "Shallow reef crests, high wave energy zones",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 10);

        yield return BahamianSpecies.Create(
            scientificName: "Acropora cervicornis",
            commonName: "Staghorn Coral",
            category: SpeciesCategory.Coral,
            conservationStatus: ConservationStatus.CriticallyEndangered,
            localName: "Staghorn",
            description: "Fast-growing branching coral. Population declined over 95%. Key habitat for juvenile fish.",
            identificationTips: "Cylindrical branches like deer antlers. Golden to pale brown. Branches 1-2cm diameter.",
            habitat: "Back reef, lagoons, moderate wave energy",
            typicalDepthMinM: 1,
            typicalDepthMaxM: 30);

        // CRITICALLY ENDANGERED FISH
        yield return BahamianSpecies.Create(
            scientificName: "Epinephelus striatus",
            commonName: "Nassau Grouper",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.CriticallyEndangered,
            localName: "Nassau Grouper",
            description: "Iconic Bahamian reef fish. Population crashed due to overfishing of spawning aggregations. Protected in Bahamas.",
            identificationTips: "Large grouper with 5 dark vertical bars. Dark stripe through eye. Can change color. Up to 1.2m long.",
            habitat: "Coral reefs, rocky areas, seagrass beds",
            typicalDepthMinM: 1,
            typicalDepthMaxM: 90);

        // ENDANGERED SEA TURTLES
        yield return BahamianSpecies.Create(
            scientificName: "Eretmochelys imbricata",
            commonName: "Hawksbill Sea Turtle",
            category: SpeciesCategory.Reptile,
            conservationStatus: ConservationStatus.CriticallyEndangered,
            localName: "Hawksbill",
            description: "Critically endangered sea turtle. Key sponge predator on coral reefs. Protected under Bahamas law.",
            identificationTips: "Narrow pointed beak. Overlapping scutes on shell. Beautiful tortoiseshell pattern.",
            habitat: "Coral reefs, rocky areas, mangroves",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 70);

        yield return BahamianSpecies.Create(
            scientificName: "Chelonia mydas",
            commonName: "Green Sea Turtle",
            category: SpeciesCategory.Reptile,
            conservationStatus: ConservationStatus.Endangered,
            localName: "Green Turtle",
            description: "Large sea turtle. Important grazer maintaining healthy seagrass beds. Multiple nesting beaches in Bahamas.",
            identificationTips: "Oval shell, greenish-brown. Single pair of prefrontal scales. Smooth shell edges.",
            habitat: "Seagrass beds, coral reefs, nesting beaches",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 40);

        yield return BahamianSpecies.Create(
            scientificName: "Caretta caretta",
            commonName: "Loggerhead Sea Turtle",
            category: SpeciesCategory.Reptile,
            conservationStatus: ConservationStatus.Vulnerable,
            localName: "Loggerhead",
            description: "Large-headed sea turtle. Important predator of mollusks and crustaceans.",
            identificationTips: "Large head with powerful jaws. Reddish-brown shell. Two pairs of prefrontal scales.",
            habitat: "Nearshore waters, coral reefs, open ocean",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 200);

        // VULNERABLE COMMERCIAL SPECIES
        yield return BahamianSpecies.Create(
            scientificName: "Strombus gigas",
            commonName: "Queen Conch",
            category: SpeciesCategory.Invertebrate,
            conservationStatus: ConservationStatus.Vulnerable,
            localName: "Conch",
            description: "Iconic Bahamian mollusk. Heavily regulated due to overfishing. National symbol.",
            identificationTips: "Large spiral shell with flared pink lip. Can reach 30cm. Shell covered in algae when alive.",
            habitat: "Seagrass beds, sand flats, coral rubble",
            typicalDepthMinM: 1,
            typicalDepthMaxM: 25);

        yield return BahamianSpecies.Create(
            scientificName: "Panulirus argus",
            commonName: "Caribbean Spiny Lobster",
            category: SpeciesCategory.Invertebrate,
            conservationStatus: ConservationStatus.DataDeficient,
            localName: "Crawfish",
            description: "Commercially important lobster. Strict fishing seasons apply. Major export product.",
            identificationTips: "Long spiny antennae, no claws. Brown-orange with yellow spots. Up to 60cm length.",
            habitat: "Coral reefs, rocky crevices, seagrass",
            typicalDepthMinM: 1,
            typicalDepthMaxM: 90);

        // SHARKS (Protected in Bahamas Shark Sanctuary)
        yield return BahamianSpecies.Create(
            scientificName: "Carcharhinus perezi",
            commonName: "Caribbean Reef Shark",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.Endangered,
            localName: "Reef Shark",
            description: "Most common shark on Bahamian reefs. Protected under Bahamas Shark Sanctuary (2011).",
            identificationTips: "Grey above, white below. Rounded snout. Interdorsal ridge. Up to 3m.",
            habitat: "Coral reefs, reef edges, drop-offs",
            typicalDepthMinM: 1,
            typicalDepthMaxM: 380);

        yield return BahamianSpecies.Create(
            scientificName: "Negaprion brevirostris",
            commonName: "Lemon Shark",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.Vulnerable,
            localName: "Lemon Shark",
            description: "Yellow-brown shark common in mangroves. Important nursery habitat in Bimini.",
            identificationTips: "Yellowish-brown color. Two dorsal fins of similar size. Blunt snout.",
            habitat: "Mangroves, shallow bays, seagrass",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 92);

        yield return BahamianSpecies.Create(
            scientificName: "Galeocerdo cuvier",
            commonName: "Tiger Shark",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.NearThreatened,
            localName: "Tiger Shark",
            description: "Large apex predator. Tiger stripes fade with age. Important at Tiger Beach diving site.",
            identificationTips: "Dark stripes on sides (fade with age). Blunt snout. Large size up to 5m.",
            habitat: "Coastal waters, reef edges, open ocean",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 350);

        // MARINE MAMMALS
        yield return BahamianSpecies.Create(
            scientificName: "Tursiops truncatus",
            commonName: "Atlantic Bottlenose Dolphin",
            category: SpeciesCategory.Mammal,
            conservationStatus: ConservationStatus.LeastConcern,
            localName: "Dolphin",
            description: "Common dolphin in Bahamas waters. Popular with tour operators. Year-round resident.",
            identificationTips: "Grey above, lighter below. Curved dorsal fin. Distinctive bottle-shaped snout.",
            habitat: "Coastal waters, banks, deep water",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 300);

        yield return BahamianSpecies.Create(
            scientificName: "Stenella frontalis",
            commonName: "Atlantic Spotted Dolphin",
            category: SpeciesCategory.Mammal,
            conservationStatus: ConservationStatus.LeastConcern,
            localName: "Spotted Dolphin",
            description: "Frequently encountered on Bahamas banks. Known for human interaction at White Sand Ridge.",
            identificationTips: "Spots develop with age. Juveniles unspotted. Slender body with long beak.",
            habitat: "Shallow banks, sand flats, deep water",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 200);

        // RAYS
        yield return BahamianSpecies.Create(
            scientificName: "Hypanus americanus",
            commonName: "Southern Stingray",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.NearThreatened,
            localName: "Stingray",
            description: "Common ray on sandy bottoms. Frequent at Stingray City, Grand Bahama.",
            identificationTips: "Diamond-shaped disc. Grey-brown above, white below. Long whip-like tail with spine.",
            habitat: "Sandy bottoms, seagrass beds, shallow lagoons",
            typicalDepthMinM: 0,
            typicalDepthMaxM: 53);

        // COMMON REEF FISH
        yield return BahamianSpecies.Create(
            scientificName: "Pomacanthus paru",
            commonName: "French Angelfish",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.LeastConcern,
            localName: "French Angel",
            description: "Beautiful reef fish. Often seen in pairs. Important for reef tourism.",
            identificationTips: "Black with yellow scale edges. Yellow face markings. Juveniles black with yellow stripes.",
            habitat: "Coral reefs, rocky areas, sponge gardens",
            typicalDepthMinM: 2,
            typicalDepthMaxM: 100);

        yield return BahamianSpecies.Create(
            scientificName: "Holocanthus ciliaris",
            commonName: "Queen Angelfish",
            category: SpeciesCategory.Fish,
            conservationStatus: ConservationStatus.LeastConcern,
            localName: "Queen Angel",
            description: "Stunning blue and yellow angelfish. Popular with underwater photographers.",
            identificationTips: "Bright blue with yellow highlights. Blue crown spot with blue ring. Up to 45cm.",
            habitat: "Coral reefs, gorgonian areas",
            typicalDepthMinM: 2,
            typicalDepthMaxM: 70);
    }
}
