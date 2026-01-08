import { TestBed } from '@angular/core/testing';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should expose the title signal', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance as any;
    // `title` is an Angular signal -> call it to get the current value
    expect(typeof app.title).toBe('function');
    expect(app.title()).toBe('deployment-risk-ui');
  });

  it('should render router outlet in template', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.innerHTML).toContain('router-outlet');
  });
});
