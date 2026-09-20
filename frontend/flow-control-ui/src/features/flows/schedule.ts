export const WEEK_DAYS = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday'
] as const;

export type WeekDay = (typeof WEEK_DAYS)[number];
export interface SchedulePeriod {
  on: string;
  off: string;
}
export type WeeklySchedule = Record<WeekDay, SchedulePeriod[]>;

export const emptyWeeklySchedule = (): WeeklySchedule => ({
  monday: [],
  tuesday: [],
  wednesday: [],
  thursday: [],
  friday: [],
  saturday: [],
  sunday: []
});

const isTime = (value: unknown): value is string =>
  typeof value === 'string' && /^(?:[01]\d|2[0-3]):[0-5]\d$/.test(value);

export const parseWeeklySchedule = (value: unknown): WeeklySchedule => {
  if (typeof value !== 'string') return emptyWeeklySchedule();
  try {
    const source = JSON.parse(value) as Record<string, unknown>;
    const result = emptyWeeklySchedule();
    for (const day of WEEK_DAYS) {
      if (!Array.isArray(source[day])) continue;
      result[day] = source[day]
        .filter(
          (period): period is SchedulePeriod =>
            typeof period === 'object' &&
            period !== null &&
            isTime((period as SchedulePeriod).on) &&
            isTime((period as SchedulePeriod).off)
        )
        .map((period) => ({ ...period }));
    }
    return result;
  } catch {
    return emptyWeeklySchedule();
  }
};

export const schedulePeriodError = (
  periods: SchedulePeriod[],
  candidate: SchedulePeriod,
  editingIndex = -1
): string | undefined => {
  if (!isTime(candidate.on) || !isTime(candidate.off)) return 'Choose an on and off time.';
  if (candidate.on === candidate.off) return 'On and off cannot occur in the same minute.';
  const others = periods.filter((_, index) => index !== editingIndex);
  if (others.some((period) => [period.on, period.off].includes(candidate.on)))
    return 'Another change already occurs at the on minute.';
  if (others.some((period) => [period.on, period.off].includes(candidate.off)))
    return 'Another change already occurs at the off minute.';
  const candidateEnd =
    candidate.off > candidate.on
      ? candidate.off
      : `${Number(candidate.off.slice(0, 2)) + 24}:${candidate.off.slice(3)}`;
  if (
    others.some((period) => {
      const periodEnd =
        period.off > period.on
          ? period.off
          : `${Number(period.off.slice(0, 2)) + 24}:${period.off.slice(3)}`;
      return candidate.on < periodEnd && candidateEnd > period.on;
    })
  )
    return 'This period overlaps another period.';
};

const minuteOfDay = (value: string): number =>
  Number(value.slice(0, 2)) * 60 + Number(value.slice(3));

export const weeklyScheduleErrors = (schedule: WeeklySchedule): Record<string, string> => {
  const errors: Record<string, string> = {};
  const weekMinutes = 7 * 24 * 60;
  const entries = WEEK_DAYS.flatMap((day, dayIndex) =>
    schedule[day].map((period, index) => {
      const start = dayIndex * 1_440 + minuteOfDay(period.on);
      let end = dayIndex * 1_440 + minuteOfDay(period.off);
      if (end <= start) end += 1_440;
      return { key: `${day}-${index}`, start, end };
    })
  );

  for (const day of WEEK_DAYS) {
    schedule[day].forEach((period, index) => {
      const error = schedulePeriodError(schedule[day], period, index);
      if (error) errors[`${day}-${index}`] = error;
    });
  }

  entries.forEach((entry, index) => {
    for (const other of entries.slice(index + 1)) {
      const entryTransitions = [entry.start % weekMinutes, entry.end % weekMinutes];
      const otherTransitions = [other.start % weekMinutes, other.end % weekMinutes];
      if (entryTransitions.some((minute) => otherTransitions.includes(minute))) {
        errors[entry.key] = 'Another change already occurs at this minute.';
        errors[other.key] = 'Another change already occurs at this minute.';
        continue;
      }
      if (
        [-weekMinutes, 0, weekMinutes].some(
          (shift) => entry.start < other.end + shift && entry.end > other.start + shift
        )
      ) {
        errors[entry.key] = 'This period overlaps another period.';
        errors[other.key] = 'This period overlaps another period.';
      }
    }
  });

  return errors;
};

export const serializeWeeklySchedule = (schedule: WeeklySchedule): string =>
  JSON.stringify(
    Object.fromEntries(
      WEEK_DAYS.map((day) => [day, [...schedule[day]].sort((a, b) => a.on.localeCompare(b.on))])
    )
  );
