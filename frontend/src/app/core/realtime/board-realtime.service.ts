import { Injectable, InjectionToken, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { BehaviorSubject, Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { RuntimeConfigService } from '../config/runtime-config.service';

export type BoardEventName = 'TaskCreated' | 'TaskUpdated' | 'TaskDeleted' | 'TaskMoved' | 'ColumnChanged' | 'PresenceChanged';
export interface BoardEvent { name: BoardEventName; payload: unknown; }
export type RealtimeState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting' | 'error';

export const BOARD_CONNECTION_FACTORY = new InjectionToken<(url: string, token: string) => HubConnection>('BOARD_CONNECTION_FACTORY', {
  providedIn: 'root',
  factory: () => (url, token) => new HubConnectionBuilder()
    .withUrl(url, { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()
});

const eventNames: readonly BoardEventName[] = ['TaskCreated', 'TaskUpdated', 'TaskDeleted', 'TaskMoved', 'ColumnChanged', 'PresenceChanged'];

@Injectable({ providedIn: 'root' })
export class BoardRealtimeService {
  private connection?: HubConnection;
  private boardId?: string;
  private generation = 0;
  private readonly connectionFactory = inject(BOARD_CONNECTION_FACTORY);
  private readonly eventSubject = new Subject<BoardEvent>();
  private readonly reconnectedSubject = new Subject<void>();
  private readonly resubscribingSubject = new Subject<void>();
  private readonly stateSubject = new BehaviorSubject<RealtimeState>('disconnected');
  readonly events$ = this.eventSubject.asObservable();
  readonly reconnected$ = this.reconnectedSubject.asObservable();
  readonly resubscribing$ = this.resubscribingSubject.asObservable();
  readonly state$ = this.stateSubject.asObservable();

  constructor(private readonly config: RuntimeConfigService, private readonly auth: AuthService) {}

  async connect(boardId: string): Promise<void> {
    const generation = ++this.generation;
    const previous = this.connection;
    const previousBoard = this.boardId;
    this.connection = undefined;
    this.boardId = undefined;
    this.stateSubject.next('connecting');
    await this.cleanup(previous, previousBoard);
    if (generation !== this.generation) return;

    const token = this.auth.token();
    if (!token) {
      this.stateSubject.next('error');
      throw new Error('No hay una sesión válida para conectar el tablero.');
    }

    const connection = this.connectionFactory(this.config.hubUrl, token);
    this.connection = connection;
    this.boardId = boardId;
    eventNames.forEach(name => connection.on(name, payload => {
      if (this.isCurrent(connection, boardId, generation)) this.eventSubject.next({ name, payload });
    }));
    connection.onreconnecting(() => {
      if (this.isCurrent(connection, boardId, generation)) this.stateSubject.next('reconnecting');
    });
    connection.onreconnected(() => {
      if (!this.isCurrent(connection, boardId, generation)) return;
      this.stateSubject.next('reconnecting');
      this.resubscribingSubject.next();
      void this.resubscribe(connection, boardId, generation);
    });
    connection.onclose(() => {
      if (this.isCurrent(connection, boardId, generation)) this.stateSubject.next('disconnected');
    });

    try {
      await connection.start();
      if (!this.isCurrent(connection, boardId, generation)) {
        await this.cleanup(connection, boardId);
        return;
      }
      await connection.invoke('SubscribeToBoard', boardId);
      if (this.isCurrent(connection, boardId, generation)) this.stateSubject.next('connected');
    } catch (error) {
      if (this.isCurrent(connection, boardId, generation)) {
        this.connection = undefined;
        this.boardId = undefined;
        this.stateSubject.next('error');
      }
      await this.cleanup(connection, boardId);
      throw error;
    }
  }

  async stop(): Promise<void> {
    const generation = ++this.generation;
    const connection = this.connection;
    const boardId = this.boardId;
    this.connection = undefined;
    this.boardId = undefined;
    await this.cleanup(connection, boardId);
    if (generation === this.generation) this.stateSubject.next('disconnected');
  }

  private isCurrent(connection: HubConnection, boardId: string, generation: number): boolean {
    return generation === this.generation && this.connection === connection && this.boardId === boardId;
  }

  private async cleanup(connection?: HubConnection, boardId?: string): Promise<void> {
    if (!connection) return;
    eventNames.forEach(name => connection.off(name));
    if (connection.state === HubConnectionState.Connected && boardId) {
      try { await connection.invoke('UnsubscribeFromBoard', boardId); } catch { /* The socket may already be closing. */ }
    }
    try { await connection.stop(); } catch { /* Cleanup is best effort. */ }
  }

  private async resubscribe(connection: HubConnection, boardId: string, generation: number): Promise<void> {
    let delay = 0;
    while (this.isCurrent(connection, boardId, generation) && connection.state === HubConnectionState.Connected) {
      if (delay) await new Promise(resolve => setTimeout(resolve, delay));
      if (!this.isCurrent(connection, boardId, generation) || connection.state !== HubConnectionState.Connected) return;
      try {
        await connection.invoke('SubscribeToBoard', boardId);
        if (!this.isCurrent(connection, boardId, generation)) return;
        this.stateSubject.next('connected');
        this.reconnectedSubject.next();
        return;
      } catch {
        delay = Math.min(delay ? delay * 2 : 2000, 10000);
      }
    }
  }
}
