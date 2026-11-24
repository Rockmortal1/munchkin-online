import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';

export interface Card {
  id: number;
  packId: number;
  name: string;
  deckType: 'door' | 'treasure';
  cardType: string;
  description: string;
  imageUrl?: string | null;
  stats: string; // JSON string que viene de PostgreSQL
}

export interface PlayerState {
  connectionId: string;
  username: string;
  level: number;
  gearBonus: number;
  race: string;
  class: string;
  handCardIds: number[];
  equippedCardIds: number[];
  stolenTargetsThisTurn: string[];
  goldBank: number;
}

export interface GameRoom {
  roomCode: string;
  hostConnectionId: string;
  isStarted: boolean;
  currentTurnIndex: number;
  hasKickedDoor: boolean;
  players: PlayerState[];
  doorDeck: number[];
  treasureDeck: number[];
  doorDiscard: number[];
  treasureDiscard: number[];
  activeCombat?: CombatState | null;
  actionLog: string[];
  winnerUsername?: string | null;
  activeTrade?: TradeState | null;
  hasFoughtThisTurn: boolean;
  hasLootedRoom: boolean;
}

export interface TradeState {
  proposerConnectionId: string;
  targetConnectionId: string;
  proposerCardIds: number[];
  targetCardIds: number[];
  proposerAccepted: boolean;
  targetAccepted: boolean;
}

export interface CombatState {
  activePlayerConnectionId: string;
  helperConnectionId?: string;
  monsterCardIds: number[];
  monsterBaseLevel: number;
  monsterTreasures: number;
  monsterLevelsGained: number;
  playerSideModifier: number;
  monsterSideModifier: number;
  helperTreasureShare: number;
  pendingHelperConnectionId?: string | null;
  pendingHelperTreasures: number;
}

export interface SeasonStats {
  season: string;
  leaderboard: { username: string; wins: number }[];
  recent: { id: number; seasonName: string; winnerUsername: string; playersList: string; playedAt: string }[];
}

@Injectable({
  providedIn: 'root'
})
export class GameHubService {
  private hubConnection!: signalR.HubConnection;
  private readonly apiUrl = window.location.hostname === 'localhost'
    ? 'https://localhost:7105'
    : 'https://TU-BACKEND-EN-RENDER.onrender.com';

  // Estado reactivo en tiempo real
  room = signal<GameRoom | null>(null);
  isConnected = signal<boolean>(false);
  myConnectionId = signal<string | null>(null);
  errorMessage = signal<string | null>(null);
  cardsMap = signal(new Map<number, Card>());

  async startConnection(): Promise<void> {
    if (this.hubConnection) return;

    await this.loadCardsCatalog();

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.apiUrl}/hubs/game`)
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('RoomUpdated', (updatedRoom: GameRoom) => {
      this.room.set(updatedRoom);
      this.errorMessage.set(null);
    });

    this.hubConnection.on('Error', (msg: string) => {
      this.errorMessage.set(msg);
    });

    try {
      await this.hubConnection.start();
      this.isConnected.set(true);
      this.myConnectionId.set(this.hubConnection.connectionId);
    } catch (err) {
      console.error('Error conectando a SignalR:', err);
      this.errorMessage.set('No se pudo conectar al servidor. Revisá que el backend esté corriendo.');
    }
  }

  private async loadCardsCatalog(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/api/cards`);
      const cards: Card[] = await response.json();
      const map = new Map<number, Card>();
      cards.forEach(c => map.set(c.id, c));
      this.cardsMap.set(map);
    } catch (err) {
      console.error('Error cargando catálogo de cartas:', err);
    }
  }

  async createRoom(username: string) {
    await this.hubConnection.invoke('CreateRoom', username);
  }

  async joinRoom(roomCode: string, username: string) {
    await this.hubConnection.invoke('JoinRoom', roomCode.toUpperCase(), username);
  }

  async startGame(roomCode: string) {
    await this.hubConnection.invoke('StartGame', roomCode);
  }

  async rollDice(roomCode: string) {
    await this.hubConnection.invoke('RollDice', roomCode);
  }

  async changeLevel(roomCode: string, delta: number) {
    await this.hubConnection.invoke('ChangeLevel', roomCode, delta);
  }

  async playCard(roomCode: string, cardId: number, targetConnectionId: string | null = null) {
    await this.hubConnection.invoke('PlayCard', roomCode, cardId, targetConnectionId);
  }

  async useEquippedCard(roomCode: string, cardId: number) {
    await this.hubConnection.invoke('UseEquippedCard', roomCode, cardId);
  }

  async discardCard(roomCode: string, cardId: number, fromEquipped: boolean) {
    await this.hubConnection.invoke('DiscardCard', roomCode, cardId, fromEquipped);
  }

  async kickDoor(roomCode: string) {
    await this.hubConnection.invoke('KickDoor', roomCode);
  }

  async lookForTrouble(roomCode: string, cardId: number) {
    await this.hubConnection.invoke('LookForTrouble', roomCode, cardId);
  }

  async playCombatModifier(roomCode: string, cardId: number, side: 'player' | 'monster', fromEquipped: boolean) {
    await this.hubConnection.invoke('PlayCombatModifier', roomCode, cardId, side, fromEquipped);
  }

  async adjustCombatModifier(roomCode: string, side: 'player' | 'monster', delta: number) {
    await this.hubConnection.invoke('AdjustCombatModifier', roomCode, side, delta);
  }

  async resolveCombat(roomCode: string) {
    await this.hubConnection.invoke('ResolveCombat', roomCode);
  }

  async runAway(roomCode: string) {
    await this.hubConnection.invoke('RunAway', roomCode);
  }

  async endTurn(roomCode: string) {
    await this.hubConnection.invoke('EndTurn', roomCode);
  }

  async thiefSteal(roomCode: string, discardCardId: number, targetConnectionId: string, targetCardId: number) {
  await this.hubConnection.invoke('ThiefSteal', roomCode, discardCardId, targetConnectionId, targetCardId);
}

async startTrade(roomCode: string, targetConnectionId: string) {
    await this.hubConnection.invoke('StartTrade', roomCode, targetConnectionId);
  }

  async toggleTradeCard(roomCode: string, cardId: number) {
    await this.hubConnection.invoke('ToggleTradeCard', roomCode, cardId);
  }

  async acceptTrade(roomCode: string) {
    await this.hubConnection.invoke('AcceptTrade', roomCode);
  }

  async cancelTrade(roomCode: string) {
    await this.hubConnection.invoke('CancelTrade', roomCode);
  }

  async uploadCardsCsv(file: File, replaceAll = false): Promise<{ imported: number; skipped: number }> {
    const formData = new FormData();
    formData.append('file', file);

    const response = await fetch(`${this.apiUrl}/api/cards/import-csv?replaceAll=${replaceAll}`, {
      method: 'POST',
      body: formData
    });

    if (!response.ok) throw new Error('Error al importar el CSV');
    const result = await response.json();

    // Recargar el catálogo en memoria inmediatamente
    await this.loadCardsCatalog();
    return result;
  }

  async requestCombatHelp(roomCode: string, targetConnectionId: string, offeredTreasures: number) {
    await this.hubConnection.invoke('RequestCombatHelp', roomCode, targetConnectionId, offeredTreasures);
  }

  async respondCombatHelp(roomCode: string, accept: boolean) {
    await this.hubConnection.invoke('RespondCombatHelp', roomCode, accept);
  }

  async sellCards(roomCode: string, cardIds: number[]) {
    await this.hubConnection.invoke('SellCards', roomCode, cardIds);
  }

  async buyLevelWithGold(roomCode: string) {
  await this.hubConnection.invoke('BuyLevelWithGold', roomCode);
  }

  seasonStats = signal<SeasonStats | null>(null);

  async loadSeasonStats(season = 'Temporada 1') {
    try {
      const res = await fetch(`${this.apiUrl}/api/matches?season=${encodeURIComponent(season)}`);
      if (res.ok) this.seasonStats.set(await res.json());
    } catch { }
  }

  leaveRoom() {
    this.room.set(null);
    this.loadSeasonStats();
  }

  async lootRoom(roomCode: string) {
    await this.hubConnection.invoke('LootRoom', roomCode);
  }

  async addWanderingMonster(roomCode: string, cardId: number) {
    await this.hubConnection.invoke('AddWanderingMonster', roomCode, cardId);
  }

  async playSpecialCombatCard(
    roomCode: string,
    cardId: number,
    extraMonsterCardId?: number | null,
    targetConnectionId?: string | null
  ) {
    await this.hubConnection.invoke('PlaySpecialCombatCard', roomCode, cardId, extraMonsterCardId ?? null, targetConnectionId ?? null);
  }
}