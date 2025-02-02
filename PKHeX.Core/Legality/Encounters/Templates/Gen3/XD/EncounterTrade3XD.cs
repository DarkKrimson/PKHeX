using System;

namespace PKHeX.Core;

/// <summary>
/// Generation 3 Static Encounter
/// </summary>
public sealed record EncounterTrade3XD : IEncounterable, IEncounterMatch, IEncounterConvertible<XK3>, IRandomCorrelation, IFixedTrainer, IFixedNickname, IFatefulEncounterReadOnly, IMoveset
{
    public int Generation => 3;
    public EntityContext Context => EntityContext.Gen3;
    public GameVersion Version => GameVersion.XD;
    int ILocation.EggLocation => 0;
    int ILocation.Location => Location;
    public bool IsShiny => false;
    private bool Gift => FixedBall == Ball.Poke;
    public Shiny Shiny => Shiny.Random;
    public AbilityPermission Ability => AbilityPermission.Any12;
    public bool FatefulEncounter => true;

    public bool IsFixedTrainer => true;
    public bool IsFixedNickname => Nicknames.Length > 0;
    public ushort Species { get; }
    public byte Level { get; }

    public Ball FixedBall => Ball.Poke;

    public required byte Location { get; init; }
    public byte Form => 0;
    public bool EggEncounter => false;
    public required Moveset Moves { get; init; }
    public required ushort TID16 { get; init; }
    // SID: Based on player ID

    private readonly string[] TrainerNames;

    private readonly string[] Nicknames;

    public EncounterTrade3XD(ushort species, byte level, string[] trainer) : this(species, level, trainer, []) { }

    public EncounterTrade3XD(ushort species, byte level, string[] trainer, string[] nicknames)
    {
        Species = species;
        Level = level;
        TrainerNames = trainer;
        Nicknames = nicknames;
    }

    public string Name => "Trade Encounter";
    public string LongName => Name;
    public byte LevelMin => Level;
    public byte LevelMax => Level;

    #region Generating
    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria) => ConvertToPKM(tr, criteria);
    PKM IEncounterConvertible.ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr);
    public XK3 ConvertToPKM(ITrainerInfo tr) => ConvertToPKM(tr, EncounterCriteria.Unrestricted);

    public XK3 ConvertToPKM(ITrainerInfo tr, EncounterCriteria criteria)
    {
        int lang = GetTemplateLanguage(tr);
        var pi = PersonalTable.E[Species];
        var pk = new XK3
        {
            Species = Species,
            CurrentLevel = LevelMin,
            OT_Friendship = pi.BaseFriendship,

            Met_Location = Location,
            Met_Level = Level,
            Version = (byte)GameVersion.CXD,
            Ball = (byte)Ball.Poke,
            FatefulEncounter = FatefulEncounter,
            Language = lang,
            OT_Name = TrainerNames[lang],
            OT_Gender = 0,
            TID16 = TID16,
            SID16 = tr.SID16,
            Nickname = IsFixedNickname ? GetNickname(lang) : SpeciesName.GetSpeciesNameGeneration(Species, lang, Generation),
        };

        SetPINGA(pk, criteria, pi);
        if (Moves.HasMoves)
            pk.SetMoves(Moves);
        else
            EncounterUtil1.SetEncounterMoves(pk, Version, Level);

        pk.ResetPartyStats();
        return pk;
    }

    private int GetTemplateLanguage(ITrainerInfo tr) => (int)Language.GetSafeLanguage(Generation, (LanguageID)tr.Language);

    private void SetPINGA(XK3 pk, EncounterCriteria criteria, PersonalInfo3 pi)
    {
        int gender = criteria.GetGender(pi);
        int nature = (int)criteria.GetNature();
        var ability = criteria.GetAbilityFromNumber(Ability);
        if (Species == (int)Core.Species.Unown)
        {
            do
            {
                PIDGenerator.SetRandomWildPID4(pk, nature, ability, gender, PIDType.Method_1_Unown);
                ability ^= 1; // some nature-forms cannot have a certain PID-ability set, so just flip it as Unown doesn't have dual abilities.
            } while (pk.Form != Form);
        }
        else
        {
            const PIDType type = PIDType.CXD;
            do
            {
                PIDGenerator.SetRandomWildPID4(pk, nature, ability, gender, type);
            } while (Shiny == Shiny.Never && pk.IsShiny);
        }
    }
    #endregion

    #region Matching
    public bool IsMatchExact(PKM pk, EvoCriteria evo)
    {
        if (!IsMatchEggLocation(pk))
            return false;
        if (!IsMatchLocation(pk))
            return false;
        if (!IsMatchLevel(pk, evo))
            return false;
        if (pk.TID16 != TID16) // SID is from player!
            return false;
        if (Form != evo.Form && !FormInfo.IsFormChangeable(Species, Form, pk.Form, Context, pk.Context))
            return false;
        return true;
    }

    public EncounterMatchRating GetMatchRating(PKM pk)
    {
        if (IsMatchPartial(pk))
            return EncounterMatchRating.PartialMatch;
        return EncounterMatchRating.Match;
    }

    private static bool IsMatchEggLocation(PKM pk)
    {
        if (pk.Format == 3)
            return true;

        var expect = pk is PB8 ? Locations.Default8bNone : 0;
        return pk.Egg_Location == expect;
    }

    private bool IsMatchLevel(PKM pk, EvoCriteria evo)
    {
        if (pk.Format != 3) // Met Level lost on PK3=>PK4
            return evo.LevelMax >= Level;
        return pk.Met_Level == Level;
    }

    private bool IsMatchLocation(PKM pk)
    {
        if (pk.Format != 3)
            return true; // transfer location verified later

        var met = pk.Met_Location;
        return Location == met;
    }

    private bool IsMatchPartial(PKM pk)
    {
        if (Gift && pk.Ball != (byte)FixedBall)
            return true;
        return false;
    }
    #endregion

    public bool IsCompatible(PIDType val, PKM pk) => val is PIDType.CXD;
    public PIDType GetSuggestedCorrelation() => PIDType.CXD;
    public bool IsTrainerMatch(PKM pk, ReadOnlySpan<char> trainer, int language) => (uint)language < TrainerNames.Length && trainer.SequenceEqual(TrainerNames[language]);

    public bool IsNicknameMatch(PKM pk, ReadOnlySpan<char> nickname, int language)
    {
        if (!IsFixedNickname)
            return true;
        return nickname.SequenceEqual(GetNickname(language));
    }

    public string GetNickname(int language) => (uint)language < Nicknames.Length ? Nicknames[language] : Nicknames[0];
}
