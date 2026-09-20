import { describe, expect, it } from 'vitest';

import {
  emptyWeeklySchedule,
  parseWeeklySchedule,
  schedulePeriodError,
  serializeWeeklySchedule,
  weeklyScheduleErrors
} from '@/features/flows/schedule';

describe('weekly schedule', () => {
  it('round trips independent periods for every day', () => {
    const schedule = emptyWeeklySchedule();
    schedule.monday = [
      { on: '08:15', off: '09:30' },
      { on: '17:00', off: '18:45' }
    ];
    schedule.sunday = [{ on: '11:01', off: '11:02' }];

    expect(parseWeeklySchedule(serializeWeeklySchedule(schedule))).toEqual(schedule);
  });

  it('rejects matching on and off minutes', () => {
    expect(schedulePeriodError([], { on: '09:15', off: '09:15' })).toBe(
      'On and off cannot occur in the same minute.'
    );
  });

  it('rejects an off event at the same minute as another on event', () => {
    expect(
      schedulePeriodError([{ on: '09:00', off: '10:00' }], { on: '08:00', off: '09:00' })
    ).toBe('Another change already occurs at the off minute.');
  });

  it('rejects overlaps but accepts adjacent periods at different minutes', () => {
    const periods = [{ on: '09:00', off: '10:00' }];
    expect(schedulePeriodError(periods, { on: '09:30', off: '10:30' })).toBe(
      'This period overlaps another period.'
    );
    expect(schedulePeriodError(periods, { on: '10:01', off: '11:00' })).toBeUndefined();
  });

  it('accepts an earlier off time as an overnight period', () => {
    expect(schedulePeriodError([], { on: '18:53', off: '06:00' })).toBeUndefined();
  });

  it('detects overlaps between overnight periods starting on the same day', () => {
    expect(
      schedulePeriodError([{ on: '18:53', off: '06:00' }], { on: '23:00', off: '07:00' })
    ).toBe('This period overlaps another period.');
  });

  it('detects next-day overlaps and transition conflicts', () => {
    const schedule = emptyWeeklySchedule();
    schedule.sunday = [{ on: '18:53', off: '06:00' }];
    schedule.monday = [{ on: '05:00', off: '06:00' }];

    expect(weeklyScheduleErrors(schedule)).toEqual({
      'monday-0': 'Another change already occurs at this minute.',
      'sunday-0': 'Another change already occurs at this minute.'
    });
  });

  it('recovers safely from malformed persisted data', () => {
    expect(parseWeeklySchedule('{bad')).toEqual(emptyWeeklySchedule());
  });
});
