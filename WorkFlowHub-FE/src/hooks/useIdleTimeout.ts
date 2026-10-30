import { useEffect, useRef, useCallback } from 'react';

interface UseIdleTimeoutOptions {
  enabled?: boolean;
  timeoutMs?: number; // Default: 15 minutes (15 * 60 * 1000)
  onIdle: () => void;
}

/**
 * Hook to detect user inactivity (idle timeout) and trigger an action (e.g. auto logout).
 */
export function useIdleTimeout({
  enabled = true,
  timeoutMs = 15 * 60 * 1000, // 15 minutes default
  onIdle,
}: UseIdleTimeoutOptions) {
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastActivityRef = useRef<number>(Date.now());
  const onIdleRef = useRef(onIdle);

  useEffect(() => {
    onIdleRef.current = onIdle;
  }, [onIdle]);

  const handleIdle = useCallback(() => {
    onIdleRef.current();
  }, []);

  const resetTimer = useCallback(() => {
    const now = Date.now();
    // Throttle reset if triggered within 1 second
    if (now - lastActivityRef.current < 1000) {
      return;
    }
    lastActivityRef.current = now;

    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }

    if (enabled) {
      timerRef.current = setTimeout(handleIdle, timeoutMs);
    }
  }, [enabled, timeoutMs, handleIdle]);

  useEffect(() => {
    if (!enabled) {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
        timerRef.current = null;
      }
      return;
    }

    // Set initial timer
    lastActivityRef.current = Date.now();
    timerRef.current = setTimeout(handleIdle, timeoutMs);

    // List of events indicating user activity
    const events: (keyof WindowEventMap)[] = [
      'mousedown',
      'mousemove',
      'keydown',
      'scroll',
      'touchstart',
      'click',
    ];

    const onUserActivity = () => {
      resetTimer();
    };

    events.forEach((event) => {
      window.addEventListener(event, onUserActivity, { passive: true });
    });

    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
        timerRef.current = null;
      }
      events.forEach((event) => {
        window.removeEventListener(event, onUserActivity);
      });
    };
  }, [enabled, timeoutMs, resetTimer, handleIdle]);
}
