/**
 * Shared TypeScript types for CoinDreams Cloud Functions.
 *
 * These DTOs mirror the C# domain types under Assets/Scripts/Domain/. Keep
 * them in lock-step when fields change on either side. C# references:
 *   - PlayerProfileSnapshot:        Assets/Scripts/Domain/Player/PlayerProfileSnapshot.cs
 *   - AuthoritativeDrawContracts:   Assets/Scripts/Domain/Cards/AuthoritativeDrawContracts.cs
 *   - AuthoritativeVillageUpgrade*: Assets/Scripts/Domain/Village/AuthoritativeVillageUpgradeContracts.cs
 *   - AuthoritativeStealContracts:  Assets/Scripts/Domain/Player/AuthoritativeStealContracts.cs
 *
 * Naming convention: TypeScript field names match the C# JSON serialization
 * (camelCase). PlayerProfileSnapshot uses public field names directly because
 * Unity's JsonUtility serializes them verbatim.
 */

// ---------------------------------------------------------------------------
// PlayerProfileSnapshot — root persistence DTO
// ---------------------------------------------------------------------------

/**
 * The canonical player document stored at /players/{playerId}.
 *
 * NOTE: `updatedAtUtcTicks` and `schemaVersion` are NOT yet present in the
 * C# PlayerProfileSnapshot. They are reserved here so the server can stamp
 * server-side authoritative timestamps and migrate documents safely. Add the
 * matching fields to the C# type before relying on them in production.
 */
export interface PlayerProfileSnapshot {
    playerId: string;
    revision: number;

    coins: number;

    currentEnergy: number;
    regenMaxEnergy: number;
    regenIntervalSeconds: number;
    lastRegenUtcTicks: number;

    // Shield count + cap. Server is authoritative for both. maxShields lets
    // the client UI render the correct number of shield slots dynamically;
    // changing this server-side propagates to clients on the next snapshot.
    shields: number;
    maxShields: number;

    villageLevels: number[];
    processedImpactIds: string[];

    // Server-stamped metadata (server-only for now; C# parity TODO).
    updatedAtUtcTicks: number;
    schemaVersion: number;
}

// ---------------------------------------------------------------------------
// Draw — request/response types
// ---------------------------------------------------------------------------

export enum AuthoritativeDrawEffectType {
    AddCoins = 0,
    AddEnergy = 1,
    LaunchSteal = 2,
    AddShields = 3,
}

export interface AuthoritativeDrawEffectDefinition {
    effectType: AuthoritativeDrawEffectType;
    intValue: number;
    stringValue: string;
}

export interface AuthoritativeDrawCardDefinition {
    cardId: string;
    weight: number;
    effects: AuthoritativeDrawEffectDefinition[];
}

export interface AuthoritativeDrawRequest {
    drawCost: number;
    requestedMultiplier: number; // Must be one of [1, 2, 4, 8] per C# contract.
    cards: AuthoritativeDrawCardDefinition[];
    drawId: string;
}

export enum AuthoritativeDrawStatus {
    Success = 0,
    NotEnoughEnergy = 1,
    DeckEmpty = 2,
    InvalidRequest = 3,
    Unavailable = 4,
    Error = 5,
    AlreadyProcessed = 6,
}

export interface AuthoritativeDrawResult {
    status: AuthoritativeDrawStatus;
    snapshot: PlayerProfileSnapshot | null;
    drawnCardId: string;
    stealTriggerId: string;
    message: string;
}

// ---------------------------------------------------------------------------
// Village upgrade — request/response types
// ---------------------------------------------------------------------------

export interface AuthoritativeVillageUpgradeCatalogData {
    buildingIds: string[];
    // Jagged array: upgradeCostsByBuilding[buildingIndex][upgradeStep].
    upgradeCostsByBuilding: number[][];
}

export interface AuthoritativeVillageUpgradeRequest {
    catalog: AuthoritativeVillageUpgradeCatalogData;
    buildingId: string;
    buildingIndex: number;
    useBuildingIndex: boolean;
    upgradeRequestId: string;
}

export enum BuildingUpgradeStatus {
    Success = 0,
    NotEnoughCoins = 1,
    MaxLevel = 2,
    InvalidConfiguration = 3,
    ServiceUnavailable = 4,
    UnexpectedError = 5,
}

export interface BuildingUpgradeResult {
    status: BuildingUpgradeStatus;
    buildingIndex: number;
    newLevel: number;
    coinsSpent: number;
    message: string;
}

export interface AuthoritativeVillageUpgradeResult {
    upgradeResult: BuildingUpgradeResult;
    snapshot: PlayerProfileSnapshot | null;
    message: string;
}

// ---------------------------------------------------------------------------
// Steal — request/response types
// ---------------------------------------------------------------------------

export interface AuthoritativeStealRequest {
    impactId: string;
    requestedAmount: number;
    thiefPlayerId: string;
    victimPlayerId: string;
    createdAtUtcTicks: number;
}

export enum AuthoritativeStealStatus {
    Success = 0,
    AppliedPartially = 1,
    VictimEmpty = 2,
    AlreadyApplied = 3,
    InvalidRequest = 4,
    Unavailable = 5,
    Error = 6,
}

export interface AuthoritativeStealResult {
    status: AuthoritativeStealStatus;
    thiefSnapshot: PlayerProfileSnapshot | null;
    victimSnapshot: PlayerProfileSnapshot | null;
    stolenAmount: number;
    message: string;
}

// ---------------------------------------------------------------------------
// Voodoo session — begin & stab DTOs
// ---------------------------------------------------------------------------

// beginVoodooSession takes NO request body. The server uses request.auth.uid
// as the thief, picks a random victim, and creates /stealSessions/{sessionId}.
export interface VoodooSessionBeginResult {
    sessionId: string;
    victimPlayerId: string;
    victimDisplayName: string;
    maxStabs: number;
}

export interface VoodooStabRequest {
    sessionId: string;
}

export enum VoodooStabStatus {
    Success = 0,
    SessionNotFound = 1,
    SessionExhausted = 2,
    SessionExpired = 3,
    VictimEmpty = 4,
    InvalidRequest = 5,
    Unauthorized = 6,
    Error = 7,
}

export interface VoodooStabResult {
    status: VoodooStabStatus;
    stolenAmount: number;
    stabsRemaining: number;
    isDollBroken: boolean;
    thiefSnapshot: PlayerProfileSnapshot | null;
    message: string;
}
