import { describe, it, expect, beforeEach, afterEach, mock } from 'bun:test';
import vkBridge from '@vkontakte/vk-bridge';
import { openExternalUrl } from './vkBridge';

describe('vkBridge openExternalUrl', () => {
  const originalWindow = globalThis.window;
  const originalNavigator = globalThis.navigator;

  beforeEach(() => {
    // Reset mocks on vkBridge
    vkBridge.send = mock(async () => ({ result: true })) as unknown as typeof vkBridge.send;
  });

  afterEach(() => {
    // Restore globals if modified
    globalThis.window = originalWindow;
    globalThis.navigator = originalNavigator;
  });

  it('does nothing when url is empty or undefined', () => {
    let topHref = '';
    const mockWindow = {
      self: {},
      top: { location: { set href(v: string) { topHref = v; } } },
      open: mock(() => null),
    } as unknown as Window & typeof globalThis;
    (mockWindow as unknown as { self: unknown }).self = mockWindow;

    globalThis.window = mockWindow;
    openExternalUrl('');
    expect(topHref).toBe('');
    expect(mockWindow.open).not.toHaveBeenCalled();
  });

  it('delegates to Telegram WebApp openLink when available', () => {
    let openedLink = '';
    const mockTelegram = {
      WebApp: {
        openLink: mock((url: string) => {
          openedLink = url;
        }),
      },
    };

    const mockWindow = {
      Telegram: mockTelegram,
      self: {},
      top: {},
      open: mock(() => null),
    } as unknown as Window & typeof globalThis;
    (mockWindow as unknown as { self: unknown }).self = mockWindow;
    (mockWindow as unknown as { top: unknown }).top = mockWindow;

    globalThis.window = mockWindow;

    openExternalUrl('https://vk.me/club238367404');
    expect(mockTelegram.WebApp.openLink).toHaveBeenCalledWith('https://vk.me/club238367404');
    expect(openedLink).toBe('https://vk.me/club238367404');
    expect(mockWindow.open).not.toHaveBeenCalled();
  });

  it('triggers vkBridge VKWebAppOpenURL event', () => {
    const mockWindow = {
      self: {},
      top: {},
      open: mock(() => ({ closed: false } as Window)),
    } as unknown as Window & typeof globalThis;
    (mockWindow as unknown as { self: unknown }).self = mockWindow;
    (mockWindow as unknown as { top: unknown }).top = mockWindow;

    globalThis.window = mockWindow;
    globalThis.navigator = { userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)' } as unknown as Navigator;

    openExternalUrl('https://vk.me/club238367404');

    expect(vkBridge.send).toHaveBeenCalledWith('VKWebAppOpenURL' as unknown as Parameters<typeof vkBridge.send>[0], {
      url: 'https://vk.me/club238367404',
    } as unknown as Parameters<typeof vkBridge.send>[1]);
  });

  it('navigates window.top when on iOS inside an iframe (m.vk.ru on iPhone)', () => {
    let topHref = '';
    const mockTop = {
      location: {
        set href(val: string) {
          topHref = val;
        },
        get href() {
          return topHref;
        },
      },
    };

    const mockSelf = {
      location: { href: 'https://app.pokerperm.ru/' },
      open: mock(() => null),
    };

    const mockWindow = {
      ...mockSelf,
      self: mockSelf,
      top: mockTop, // self !== top => inside iframe
    } as unknown as Window & typeof globalThis;

    globalThis.window = mockWindow;
    globalThis.navigator = {
      userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1',
      platform: 'iPhone',
      maxTouchPoints: 5,
    } as unknown as Navigator;

    openExternalUrl('https://vk.me/club238367404');

    expect(topHref).toBe('https://vk.me/club238367404');
    // On iOS inside iframe, window.open is bypassed to avoid Safari popup blocker
    expect(mockWindow.open).not.toHaveBeenCalled();
  });

  it('uses window.open on Desktop browser', () => {
    let windowOpenUrl = '';
    let windowOpenTarget = '';
    const mockPopup = { closed: false } as Window;

    const mockWindow = {
      self: {},
      top: {},
      open: mock((u: string, t: string) => {
        windowOpenUrl = u;
        windowOpenTarget = t;
        return mockPopup;
      }),
      location: { href: '' },
    } as unknown as Window & typeof globalThis;
    (mockWindow as unknown as { self: unknown }).self = mockWindow;
    (mockWindow as unknown as { top: unknown }).top = mockWindow;

    globalThis.window = mockWindow;
    globalThis.navigator = {
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
      platform: 'Win32',
      maxTouchPoints: 0,
    } as unknown as Navigator;

    openExternalUrl('https://vk.me/club238367404');

    expect(windowOpenUrl).toBe('https://vk.me/club238367404');
    expect(windowOpenTarget).toBe('_blank');
  });

  it('falls back to window.location.href when window.open is blocked on desktop', () => {
    let locationHref = '';

    const mockWindow = {
      self: {},
      top: {},
      open: mock(() => null), // blocked popup
      location: {
        set href(val: string) {
          locationHref = val;
        },
        get href() {
          return locationHref;
        },
      },
    } as unknown as Window & typeof globalThis;
    (mockWindow as unknown as { self: unknown }).self = mockWindow;
    (mockWindow as unknown as { top: unknown }).top = mockWindow;

    globalThis.window = mockWindow;
    globalThis.navigator = {
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
      platform: 'Win32',
      maxTouchPoints: 0,
    } as unknown as Navigator;

    openExternalUrl('https://vk.me/club238367404');

    expect(locationHref).toBe('https://vk.me/club238367404');
  });
});
