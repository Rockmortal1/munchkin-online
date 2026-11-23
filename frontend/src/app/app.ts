import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Card, GameHubService, PlayerState } from './services/game-hub';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  gameHub = inject(GameHubService);

  username = '';
  roomCodeInput = '';

  ngOnInit() {
    this.gameHub.startConnection();
  }

  createRoom() {
    if (!this.username.trim()) return;
    this.gameHub.createRoom(this.username.trim());
  }

  joinRoom() {
    if (!this.username.trim() || !this.roomCodeInput.trim()) return;
    this.gameHub.joinRoom(this.roomCodeInput.trim(), this.username.trim());
  }

  startGame() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.startGame(code);
  }

  rollDice() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.rollDice(code);
  }

  changeLevel(delta: number) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.changeLevel(code, delta);
  }

  isHost(): boolean {
    const room = this.gameHub.room();
    return !!room && room.hostConnectionId === this.gameHub.myConnectionId();
  }

  myPlayer(): PlayerState | undefined {
    const room = this.gameHub.room();
    const myId = this.gameHub.myConnectionId();
    return room?.players.find(p => p.connectionId === myId);
  }

  getCard(cardId: number): Card | undefined {
    return this.gameHub.cardsMap().get(cardId);
  }

  getStat(card: Card, key: string): any {
    try {
      const parsed = JSON.parse(card.stats);
      return parsed[key];
    } catch {
      return null;
    }
  }

  pendingTargetCardId: number | null = null;

  playCard(cardId: number) {
    const room = this.gameHub.room();
    const card = this.getCard(cardId);
    if (!room || !card) return;

    if (card.cardType === 'curse') {
      this.pendingTargetCardId = cardId;
      return;
    }

    if (room.activeCombat) {
      if (card.cardType === 'one_shot') {
        this.pendingCombatCard = { cardId, fromEquipped: false };
        return;
      }
      if (card.cardType === 'monster_enhancer') {
        this.gameHub.playCombatModifier(room.roomCode, cardId, 'monster', false);
        return;
      }
    }

    this.gameHub.playCard(room.roomCode, cardId, null);
  }

  confirmTarget(targetConnectionId: string) {
    const code = this.gameHub.room()?.roomCode;
    if (code && this.pendingTargetCardId !== null) {
      this.gameHub.playCard(code, this.pendingTargetCardId, targetConnectionId);
    }
    this.pendingTargetCardId = null;
  }

  cancelTarget() {
    this.pendingTargetCardId = null;
  }

  useEquippedCard(cardId: number) {
    const room = this.gameHub.room();
    if (!room) return;
    if (room.activeCombat) {
      this.pendingCombatCard = { cardId, fromEquipped: true };
      return;
    }
    this.gameHub.useEquippedCard(room.roomCode, cardId);
  }

  discardCard(cardId: number, fromEquipped = false) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.discardCard(code, cardId, fromEquipped);
  }

  // Devuelve la lista de oponentes
  opponents(): PlayerState[] {
    const room = this.gameHub.room();
    const myId = this.gameHub.myConnectionId();
    return room?.players.filter(p => p.connectionId !== myId) ?? [];
  }

  getLastDiscard(list?: number[]): Card | undefined {
    if (!list || list.length === 0) return undefined;
    return this.getCard(list[list.length - 1]);
  }

  pendingCombatCard: { cardId: number; fromEquipped: boolean } | null = null;

  currentTurnPlayer(): PlayerState | undefined {
    const room = this.gameHub.room();
    if (!room || room.players.length === 0) return undefined;
    return room.players[room.currentTurnIndex];
  }

  isMyTurn(): boolean {
    return this.currentTurnPlayer()?.connectionId === this.gameHub.myConnectionId();
  }

  kickDoor() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.kickDoor(code);
  }

  lookForTrouble(cardId: number) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.lookForTrouble(code, cardId);
  }

  resolveCombat() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.resolveCombat(code);
  }

  runAway() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.runAway(code);
  }

  endTurn() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.endTurn(code);
  }

  adjustCombat(side: 'player' | 'monster', delta: number) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.adjustCombatModifier(code, side, delta);
  }

  confirmCombatSide(side: 'player' | 'monster') {
    const code = this.gameHub.room()?.roomCode;
    if (code && this.pendingCombatCard) {
      this.gameHub.playCombatModifier(code, this.pendingCombatCard.cardId, side, this.pendingCombatCard.fromEquipped);
    }
    this.pendingCombatCard = null;
  }

  showHelpModal = false;
  helpTreasuresOffer = 1;

  getHelperPlayer(): PlayerState | undefined {
    const room = this.gameHub.room();
    const helperId = room?.activeCombat?.helperConnectionId;
    return helperId ? room.players.find(p => p.connectionId === helperId) : undefined;
  }

  getCombatPlayerPower(): number {
    const room = this.gameHub.room();
    if (!room?.activeCombat) return 0;
    const p = room.players.find(pl => pl.connectionId === room.activeCombat!.activePlayerConnectionId);
    const h = this.getHelperPlayer();
    return (p ? p.level + p.gearBonus : 0) + (h ? h.level + h.gearBonus : 0) + room.activeCombat.playerSideModifier;
  }

  requestHelp(targetConnectionId: string) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.requestCombatHelp(code, targetConnectionId, this.helpTreasuresOffer);
    this.showHelpModal = false;
  }

  respondHelp(accept: boolean) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.respondCombatHelp(code, accept);
  }

  getCombatMonsterPower(): number {
    const c = this.gameHub.room()?.activeCombat;
    return c ? c.monsterBaseLevel + c.monsterSideModifier : 0;
  }

  pendingSteal: { targetConnectionId: string; targetCardId: number; targetCardName: string } | null = null;

  openStealModal(targetConnectionId: string, targetCardId: number, targetCardName: string) {
    this.pendingSteal = { targetConnectionId, targetCardId, targetCardName };
  }

  confirmSteal(discardCardId: number) {
    const code = this.gameHub.room()?.roomCode;
    if (code && this.pendingSteal) {
      this.gameHub.thiefSteal(code, discardCardId, this.pendingSteal.targetConnectionId, this.pendingSteal.targetCardId);
    }
    this.pendingSteal = null;
  }

  startTrade(targetConnectionId: string) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.startTrade(code, targetConnectionId);
  }

  toggleTradeCard(cardId: number) {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.toggleTradeCard(code, cardId);
  }

  acceptTrade() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.acceptTrade(code);
  }

  cancelTrade() {
    const code = this.gameHub.room()?.roomCode;
    if (code) this.gameHub.cancelTrade(code);
  }

  isInActiveTrade(): boolean {
    const t = this.gameHub.room()?.activeTrade;
    const myId = this.gameHub.myConnectionId();
    return !!t && (t.proposerConnectionId === myId || t.targetConnectionId === myId);
  }

  getTradePartner(): PlayerState | undefined {
    const room = this.gameHub.room();
    const t = room?.activeTrade;
    const myId = this.gameHub.myConnectionId();
    if (!room || !t) return undefined;
    const partnerId = t.proposerConnectionId === myId ? t.targetConnectionId : t.proposerConnectionId;
    return room.players.find(p => p.connectionId === partnerId);
  }

  getMyTradeCardIds(): number[] {
    const t = this.gameHub.room()?.activeTrade;
    const myId = this.gameHub.myConnectionId();
    if (!t) return [];
    return t.proposerConnectionId === myId ? t.proposerCardIds : t.targetCardIds;
  }

  getPartnerTradeCardIds(): number[] {
    const t = this.gameHub.room()?.activeTrade;
    const myId = this.gameHub.myConnectionId();
    if (!t) return [];
    return t.proposerConnectionId === myId ? t.targetCardIds : t.proposerCardIds;
  }

  didIAcceptTrade(): boolean {
    const t = this.gameHub.room()?.activeTrade;
    const myId = this.gameHub.myConnectionId();
    if (!t) return false;
    return t.proposerConnectionId === myId ? t.proposerAccepted : t.targetAccepted;
  }

  didPartnerAcceptTrade(): boolean {
    const t = this.gameHub.room()?.activeTrade;
    const myId = this.gameHub.myConnectionId();
    if (!t) return false;
    return t.proposerConnectionId === myId ? t.targetAccepted : t.proposerAccepted;
  }

  importStatus = '';

  async onCsvSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.importStatus = '⏳ Importando cartas...';
    try {
      const res = await this.gameHub.uploadCardsCsv(file, false);
      this.importStatus = `✅ Listo: ${res.imported} cartas importadas (${res.skipped} omitidas).`;
    } catch {
      this.importStatus = '❌ Error al subir el CSV.';
    }
    input.value = '';
  }
}