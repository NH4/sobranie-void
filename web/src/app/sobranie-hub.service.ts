import { Injectable, computed, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

export interface SpeechChunk {
  chunk: string;
  done: boolean;
}

@Injectable({ providedIn: 'root' })
export class SobranieHubService {
  private connection: HubConnection | null = null;

  readonly connectionState = signal<HubConnectionState>(HubConnectionState.Disconnected);
  readonly transcript = signal<string>('');
  readonly isConnected = computed(() => this.connectionState() === HubConnectionState.Connected);

  async connect(baseUrl: string): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${baseUrl}/hub`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    this.connection.on('ReceiveSpeech', (payload: SpeechChunk) => {
      if (payload.done) {
        this.transcript.update((prev) => prev + '\n\n---\n\n');
      } else {
        this.transcript.update((prev) => prev + payload.chunk);
      }
    });

    this.connection.onreconnecting(() => this.connectionState.set(HubConnectionState.Reconnecting));
    this.connection.onreconnected(() => this.connectionState.set(HubConnectionState.Connected));
    this.connection.onclose(() => this.connectionState.set(HubConnectionState.Disconnected));

    this.connectionState.set(HubConnectionState.Connecting);
    await this.connection.start();
    this.connectionState.set(this.connection.state);
  }

  clearTranscript(): void {
    this.transcript.set('');
  }
}
