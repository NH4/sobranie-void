import { Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SobranieHubService } from './sobranie-hub.service';
import { firstValueFrom } from 'rxjs';

const ORCHESTRATOR_BASE = 'http://localhost:5139';

@Component({
  selector: 'app-root',
  imports: [],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  protected readonly hub = inject(SobranieHubService);

  protected readonly title = signal('Собрание на Република Северна Македонија');
  protected readonly lastError = signal<string | null>(null);
  protected readonly loadingProposal = signal(false);

  protected readonly sessionRunning = computed(() => this.hub.sessionStatus().running);
  protected readonly turnsCompleted = computed(() => this.hub.sessionStatus().turnsCompleted);
  protected readonly turnsPer = computed(() => this.hub.turnsPerProposal());
  protected readonly currentProposal = computed(() => this.hub.currentProposal());
  protected readonly speeches = computed(() => this.hub.speeches());
  protected readonly mainCastSpeeches = computed(() =>
    this.hub.speeches().filter((s) => s.kind === 'MainCastSpeech'),
  );
  protected readonly chorusSpeeches = computed(() =>
    this.hub.speeches().filter((s) => s.kind === 'ChorusReaction'),
  );

  async ngOnInit(): Promise<void> {
    try {
      await this.hub.connect();
    } catch (err) {
      this.lastError.set(`Failed to connect to orchestrator: ${String(err)}`);
    }
  }

  async ngOnDestroy(): Promise<void> {
    await this.hub.disconnect();
  }

  async toggleSession(): Promise<void> {
    if (this.hub.sessionStatus().running) {
      await this.hub.stopSession();
    } else {
      this.lastError.set(null);
      this.hub.clearTranscript();
      await this.hub.startSession();
    }
  }

  async triggerSmokeSpeech(): Promise<void> {
    this.lastError.set(null);
    try {
      await firstValueFrom(
        this.http.get(`${ORCHESTRATOR_BASE}/api/smoke/speak`, {
          params: {
            persona: 'Ти си пратеник во Собранието. Говориш остро, на македонски.',
            prompt: 'Коментирај ја денешната политичка состојба во 3 реченици.',
          },
          responseType: 'json',
        }),
      );
    } catch (err) {
      this.lastError.set(`Smoke call failed: ${String(err)}`);
    }
  }

  clearHansard(): void {
    this.hub.clearTranscript();
  }
}
