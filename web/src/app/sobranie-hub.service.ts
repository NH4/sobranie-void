import { Injectable, computed, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

export interface SpeechCard {
  id: number;
  mpId: string;
  mpName: string;
  partyId: string;
  partyColor: string;
  partyShort: string;
  content: string;
  tokenCount: number;
  elapsedSeconds: number;
  utteredAt: Date;
  kind: 'MainCastSpeech' | 'ChorusReaction';
  isStreaming?: boolean;
}

export interface SessionStatus {
  running: boolean;
  startedAt: string | null;
  turnsCompleted: number;
  lastError: string | null;
}

export interface Proposal {
  id: number;
  headline: string;
  source: string;
  turnsCompleted: number;
  turnsPerProposal: number;
}

const ORCHESTRATOR_BASE = 'http://localhost:5000';

const PARTIES: Record<string, { color: string; short: string }> = {
  vmro_dpmne: { color: '#C8102E', short: 'ВМРО' },
  sdsm:      { color: '#E63946', short: 'СДСМ' },
  dui:       { color: '#F2C94C', short: 'ДУИ' },
  levica:    { color: '#B71C1C', short: 'Левица' },
  alternativa:{ color: '#27AE60', short: 'Алт.' },
  vlen:      { color: '#27AE60', short: 'ВЛЕН' },
  znam:      { color: '#8E44AD', short: 'ЗНАМ' },
  alijansa:  { color: '#1E90FF', short: 'АА' },
  besa:      { color: '#00A878', short: 'БЕСА' },
  dps:       { color: '#2C3E50', short: 'ДПС' },
  nezavisni: { color: '#7F8C8D', short: 'Незав.' },
  lda:       { color: '#F4D03F', short: 'ЛДП' },
  populi:    { color: '#16A085', short: 'Попули' },
  dpa:       { color: '#3498DB', short: 'ДПА' },
  dtm:       { color: '#C0392B', short: 'ДТМ' },
  spm:       { color: '#D35400', short: 'СПМ' },
  nsdp:      { color: '#F08080', short: 'НСДП' },
};

@Injectable({ providedIn: 'root' })
export class SobranieHubService {
  private connection: HubConnection | null = null;

  readonly connectionState = signal<HubConnectionState>(HubConnectionState.Disconnected);
  readonly isConnected = computed(() => this.connectionState() === HubConnectionState.Connected);

  readonly speeches = signal<SpeechCard[]>([]);
  readonly sessionStatus = signal<SessionStatus>({ running: false, startedAt: null, turnsCompleted: 0, lastError: null });
  readonly currentProposal = signal<Proposal | null>(null);
  readonly turnsPerProposal = signal<number>(12);

  private statusPoller: ReturnType<typeof setInterval> | null = null;
  private streamingCard: SpeechCard | null = null;

  async connect(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${ORCHESTRATOR_BASE}/hub`)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: () => 3000,
      })
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('ReceiveSpeech', (payload: { mpId: string; chunk: string; done: boolean }) => {
      if (payload.done) {
        if (this.streamingCard) {
          this.speeches.update((prev) =>
            prev.map((s) => (s.id === this.streamingCard!.id ? { ...s, isStreaming: false } : s)),
          );
          this.streamingCard = null;
        }
      } else if (this.streamingCard) {
        this.speeches.update((prev) =>
          prev.map((s) => (s.id === this.streamingCard!.id ? { ...s, content: s.content + payload.chunk } : s)),
        );
      }
    });

    this.connection.on(
      'ReceiveSpeechComplete',
      (evt: {
        speechId: number;
        mpId: string;
        mpDisplayName: string;
        partyId: string;
        content: string;
        tokenCount: number;
        elapsedSeconds: number;
        utteredAt: string;
      }) => {
        const party = PARTIES[evt.partyId] ?? { color: '#555', short: '?' };
        const card: SpeechCard = {
          id: evt.speechId,
          mpId: evt.mpId,
          mpName: evt.mpDisplayName,
          partyId: evt.partyId,
          partyColor: party.color,
          partyShort: party.short,
          content: evt.content,
          tokenCount: evt.tokenCount,
          elapsedSeconds: evt.elapsedSeconds,
          utteredAt: new Date(evt.utteredAt),
          kind: 'MainCastSpeech',
          isStreaming: false,
        };
        this.speeches.update((prev) => [...prev, card]);
        this.streamingCard = card;
        this.currentProposal.update((p) => (p ? { ...p, turnsCompleted: p.turnsCompleted + 1 } : null));
      },
    );

    this.connection.on(
      'ReceiveChorusReaction',
      (payload: { content: string; partyId?: string; utteredAt: string; speechId?: number }) => {
        const party = PARTIES[payload.partyId ?? ''] ?? { color: '#444', short: 'Хор' };
        const card: SpeechCard = {
          id: payload.speechId ?? Date.now(),
          mpId: '',
          mpName: 'Хор',
          partyId: payload.partyId ?? '',
          partyColor: party.color,
          partyShort: party.short,
          content: payload.content,
          tokenCount: 0,
          elapsedSeconds: 0,
          utteredAt: new Date(payload.utteredAt),
          kind: 'ChorusReaction',
          isStreaming: false,
        };
        this.speeches.update((prev) => [...prev, card]);
      },
    );

    this.connection.onreconnecting(() => this.connectionState.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.connectionState.set(HubConnectionState.Connected));
    this.connection.onclose(() => this.connectionState.set(HubConnectionState.Disconnected));

    this.connectionState.set(HubConnectionState.Connecting);
    await this.connection.start();
    this.connectionState.set(this.connection.state);

    this.startStatusPoller();
  }

  async disconnect(): Promise<void> {
    this.stopStatusPoller();
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.connectionState.set(HubConnectionState.Disconnected);
    }
  }

  clearTranscript(): void {
    this.speeches.set([]);
  }

  async startSession(): Promise<void> {
    const res = await fetch(`${ORCHESTRATOR_BASE}/api/session/start`, { method: 'POST' });
    const json = await res.json();
    this.sessionStatus.update((s) => ({ ...s, running: json.running, startedAt: json.startedAt }));
    this.pollStatus();
  }

  async stopSession(): Promise<void> {
    const res = await fetch(`${ORCHESTRATOR_BASE}/api/session/stop`, { method: 'POST' });
    const json = await res.json();
    this.sessionStatus.update((s) => ({ ...s, running: json.running }));
  }

  private startStatusPoller(): void {
    this.pollStatus();
    this.statusPoller = setInterval(() => this.pollStatus(), 5000);
  }

  private stopStatusPoller(): void {
    if (this.statusPoller) {
      clearInterval(this.statusPoller);
      this.statusPoller = null;
    }
  }

  private async pollStatus(): Promise<void> {
    try {
      const res = await fetch(`${ORCHESTRATOR_BASE}/api/session/status`);
      if (res.ok) {
        const json: SessionStatus = await res.json();
        this.sessionStatus.set(json);
      }
    } catch {
      // ignore
    }
  }
}
