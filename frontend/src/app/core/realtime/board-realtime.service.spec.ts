import { HubConnection, HubConnectionState } from '@microsoft/signalr';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../auth/auth.service';
import { RuntimeConfigService } from '../config/runtime-config.service';
import { BOARD_CONNECTION_FACTORY, BoardRealtimeService, RealtimeState } from './board-realtime.service';

describe('BoardRealtimeService', () => {
  let connections: FakeConnection[];
  let startPromises: Promise<void>[];
  let service: BoardRealtimeService;

  beforeEach(() => {
    connections = [];
    startPromises = [];
    TestBed.configureTestingModule({ providers: [
      BoardRealtimeService,
      provideHttpClient(),
      { provide: AuthService, useValue: { token: () => 'same-api-token' } },
      { provide: BOARD_CONNECTION_FACTORY, useValue: (_url: string, token: string) => {
        const connection = new FakeConnection(token);
        connection.startPromise = startPromises.shift() ?? Promise.resolve();
        connections.push(connection);
        return connection as unknown as HubConnection;
      } }
    ] });
    TestBed.inject(RuntimeConfigService).setForTesting({ apiBaseUrl: '/api', hubUrl: '/hubs/boards', endpoints: {} });
    service = TestBed.inject(BoardRealtimeService);
  });

  it('uses the current API token and exposes connection state', async () => {
    const states: RealtimeState[] = [];
    service.state$.subscribe(state => states.push(state));
    await service.connect('board-1');
    expect(connections[0].token).toBe('same-api-token');
    expect(connections[0].invocations).toContain(['SubscribeToBoard', 'board-1']);
    expect(states).toContain('connecting');
    expect(states.at(-1)).toBe('connected');
    await service.stop();
    expect(states.at(-1)).toBe('disconnected');
  });

  it('does not subscribe a stale connection after navigation', async () => {
    const firstStart = deferred<void>();
    startPromises.push(firstStart.promise);
    const firstConnect = service.connect('board-1');
    await Promise.resolve();

    const secondConnect = service.connect('board-2');
    await secondConnect;
    firstStart.resolve();
    await firstConnect;

    expect(connections[0].invocations).not.toContain(['SubscribeToBoard', 'board-1']);
    expect(connections[1].invocations).toContain(['SubscribeToBoard', 'board-2']);
  });
});

class FakeConnection {
  state = HubConnectionState.Disconnected;
  startPromise: Promise<void> = Promise.resolve();
  readonly invocations: [string, string][] = [];
  private readonly handlers = new Map<string, ((payload: unknown) => void)[]>();

  constructor(readonly token: string) {}

  on(name: string, handler: (payload: unknown) => void): void {
    this.handlers.set(name, [...(this.handlers.get(name) ?? []), handler]);
  }
  off(name: string): void { this.handlers.delete(name); }
  onreconnecting(_handler: () => void): void {}
  onreconnected(_handler: () => void): void {}
  onclose(_handler: () => void): void {}
  async start(): Promise<void> { await this.startPromise; this.state = HubConnectionState.Connected; }
  async stop(): Promise<void> { this.state = HubConnectionState.Disconnected; }
  async invoke(method: string, boardId: string): Promise<void> { this.invocations.push([method, boardId]); }
}

function deferred<T>(): { promise: Promise<T>; resolve: (value: T) => void } {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(next => resolve = next);
  return { promise, resolve };
}
