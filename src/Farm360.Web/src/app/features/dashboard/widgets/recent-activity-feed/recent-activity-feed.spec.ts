import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RecentActivityFeed } from './recent-activity-feed';

describe('RecentActivityFeed', () => {
  let component: RecentActivityFeed;
  let fixture: ComponentFixture<RecentActivityFeed>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RecentActivityFeed]
    })
    .compileComponents();

    fixture = TestBed.createComponent(RecentActivityFeed);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
