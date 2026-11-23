import { TestBed } from '@angular/core/testing';

import { GameHubService } from './game-hub';

describe('GameHub', () => {
  let service: GameHubService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(GameHubService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
