import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { RuntimeConfigService } from '../config/runtime-config.service';

export type BoardEventName = 'TaskCreated' | 'TaskUpdated' | 'TaskDeleted' | 'TaskMoved' | 'ColumnChanged' | 'PresenceChanged';
export interface BoardEvent { name: BoardEventName; payload: unknown; }

@Injectable({ providedIn: 'root' })
export class BoardRealtimeService {
  private connection?: HubConnection;
  private boardId?: string;
  private readonly eventSubject = new Subject<BoardEvent>();
  readonly events$ = this.eventSubject.asObservable();

  constructor(private readonly config: RuntimeConfigService, private readonly auth: AuthService) {}

  async connect(boardId: string): Promise<void> {
    await this.stop();
    this.boardId = boardId;
    this.connection = new HubConnectionBuilder()
      .withUrl(this.config.hubUrl, { accessTokenFactory: () => this.auth.token() ?? '' })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    const names: BoardEventName[] = ['TaskCreated', 'TaskUpdated', 'TaskDeleted', 'TaskMoved', 'ColumnChanged', 'PresenceChanged'];
    names.forEach(name => this.connection?.on(name, payload => this.eventSubject.next({ name, payload })));
    this.connection.onreconnected(() => this.invoke('SubscribeToBoard', boardId));
    await this.connection.start();
    await this.invoke('SubscribeToBoard', boardId);
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    const boardId = this.boardId;
    this.connection = undefined;
    this.boardId = undefined;
    if (!connection) return;
    if (connection.state === HubConnectionState.Connected && boardId) {
      try { await connection.invoke('UnsubscribeFromBoard', boardId); } catch { /* The socket may already be closing. */ }
    }
    try { await connection.stop(); } catch { /* Cleanup is best effort. */ }
  }

  private async invoke(method: string, boardId: string): Promise<void> {
    await this.connection?.invoke(method, boardId);
  }
}
