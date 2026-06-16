/* eslint-disable @typescript-eslint/no-var-requires */
import * as admin from "firebase-admin";

// Point Admin SDK at the emulator BEFORE initializeApp.
process.env.FIRESTORE_EMULATOR_HOST = process.env.FIRESTORE_EMULATOR_HOST || "localhost:8080";
process.env.FIREBASE_AUTH_EMULATOR_HOST = process.env.FIREBASE_AUTH_EMULATOR_HOST || "localhost:9099";

// Project id can be anything when targeting the emulator — must match firebaserc/CLI target.
admin.initializeApp({ projectId: "coindream-2b741" });
const db = admin.firestore();

interface SeedPlayer {
    playerId: string;
    displayName: string;
    coins: number;
}

const SEED_PLAYERS: SeedPlayer[] = [
    { playerId: "test_victim_1", displayName: "Alice",  coins: 50_000 },
    { playerId: "test_victim_2", displayName: "Bob",    coins: 250_000 },
    { playerId: "test_victim_3", displayName: "Carol",  coins: 1_000_000 },
    { playerId: "test_victim_4", displayName: "Dave",   coins: 12_500 },
];

async function seed() {
    // Match the PlayerProfileSnapshot shape (functions/src/types.ts).
    for (const player of SEED_PLAYERS) {
        await db.collection("players").doc(player.playerId).set({
            playerId: player.playerId,
            displayName: player.displayName,
            revision: 1,
            coins: player.coins,
            currentEnergy: 50,
            regenMaxEnergy: 50,
            regenIntervalSeconds: 180,
            lastRegenUtcTicks: 0,
            villageLevels: [0, 0, 0, 0],
            processedImpactIds: [],
            updatedAtUtcTicks: 0,
            schemaVersion: 1,
        });
        console.log(`Seeded ${player.playerId} (${player.displayName}, ${player.coins} coins)`);
    }
    console.log(`Done. ${SEED_PLAYERS.length} players in /players.`);
}

seed().catch(err => {
    console.error("Seed failed:", err);
    process.exit(1);
});
