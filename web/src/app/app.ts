import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SobranieHubService } from './sobranie-hub.service';
import { firstValueFrom } from 'rxjs';

const ORCHESTRATOR_BASE_URL = 'http://localhost:5000';

@Component({
  selector: 'app-root',
  imports: [],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly hub = inject(SobranieHubService);

  protected readonly title = signal('Assembly of the Void');
  protected readonly busy = signal(false);
  protected readonly lastError = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    try {
      await this.hub.connect(ORCHESTRATOR_BASE_URL);
    } catch (err) {
      this.lastError.set(`Failed to connect to orchestrator: ${String(err)}`);
    }
  }

  async triggerSmokeSpeech(): Promise<void> {
    this.busy.set(true);
    this.lastError.set(null);
    this.hub.clearTranscript();

    try {
      await firstValueFrom(
        this.http.get(`${ORCHESTRATOR_BASE_URL}/api/smoke/speak`, {
          params: {
            persona: 'Ти си пратеник во Собранието. Говориш остро, на македонски.',
            prompt: 'Коментирај ја денешната политичка состојба во 3 реченици.',
          },
          responseType: 'json',
        }),
      );
    } catch (err) {
      this.lastError.set(`Smoke call failed: ${String(err)}`);
    } finally {
      this.busy.set(false);
    }
  }
}
