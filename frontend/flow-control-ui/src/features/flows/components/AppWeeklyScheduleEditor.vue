<template>
  <section class="schedule-property" aria-label="Weekly schedule">
    <strong>Weekly schedule</strong>
    <p>{{ summary }}</p>
    <button type="button" @click="open">Edit schedule</button>
  </section>

  <AppDialog
    ref="dialog"
    class="schedule-dialog"
    content-label="Edit weekly schedule"
    @cancel="cancel"
  >
    <form class="weekly-schedule" @submit.prevent="save">
      <header>
        <h2>Edit weekly schedule</h2>
        <p>Add as many on/off periods as needed. Times use one-minute resolution.</p>
      </header>

      <div class="days">
        <section v-for="day in WEEK_DAYS" :key="day" class="schedule-day">
          <div class="day-heading">
            <strong>{{ title(day) }}</strong>
            <button type="button" @click="addPeriod(day)">Add period</button>
          </div>
          <p v-if="schedule[day].length === 0" class="empty-day">No active periods</p>
          <div v-for="(period, index) in schedule[day]" :key="index" class="schedule-period">
            <label>
              <span>On</span>
              <input
                type="time"
                step="60"
                :value="period.on"
                :aria-invalid="Boolean(periodErrors[`${day}-${index}`])"
                @input="updateTime(day, index, 'on', $event)"
              />
            </label>
            <label>
              <span>Off <small v-if="crossesMidnight(period)">(next day)</small></span>
              <input
                type="time"
                step="60"
                :value="period.off"
                :aria-invalid="Boolean(periodErrors[`${day}-${index}`])"
                @input="updateTime(day, index, 'off', $event)"
              />
            </label>
            <button
              type="button"
              class="remove-period"
              :aria-label="`Remove ${title(day)} period`"
              @click="removePeriod(day, index)"
            >
              Remove
            </button>
            <small v-if="periodErrors[`${day}-${index}`]" role="alert">{{
              periodErrors[`${day}-${index}`]
            }}</small>
          </div>
        </section>
      </div>

      <footer>
        <button type="submit">Save schedule</button>
        <button type="button" @click="cancel">Cancel</button>
      </footer>
    </form>
  </AppDialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import AppDialog from '@/components/AppDialog.vue';
import {
  WEEK_DAYS,
  parseWeeklySchedule,
  serializeWeeklySchedule,
  weeklyScheduleErrors,
  type SchedulePeriod,
  type WeekDay
} from '@/features/flows/schedule';

const props = defineProps<{ value: unknown }>();
const emit = defineEmits<{ (event: 'update', value: string): void }>();
const dialog = ref<InstanceType<typeof AppDialog>>();
const schedule = reactive(parseWeeklySchedule(props.value));
const periodErrors = reactive<Record<string, string>>({});
const persistedSchedule = computed(() => parseWeeklySchedule(props.value));
const periodCount = computed(() =>
  WEEK_DAYS.reduce((total, day) => total + persistedSchedule.value[day].length, 0)
);
const activeDayCount = computed(
  () => WEEK_DAYS.filter((day) => persistedSchedule.value[day].length > 0).length
);
const summary = computed(() => {
  if (periodCount.value === 0) return 'No active periods configured.';
  return `${periodCount.value} ${periodCount.value === 1 ? 'period' : 'periods'} across ${activeDayCount.value} ${activeDayCount.value === 1 ? 'day' : 'days'}.`;
});
const title = (day: WeekDay): string => `${day[0]!.toUpperCase()}${day.slice(1)}`;
const crossesMidnight = (period: SchedulePeriod): boolean => period.off < period.on;

const resetDraft = (): void => {
  Object.assign(schedule, parseWeeklySchedule(props.value));
  for (const key of Object.keys(periodErrors)) delete periodErrors[key];
};
watch(() => props.value, resetDraft);

const open = (): void => {
  resetDraft();
  dialog.value?.showModal();
};
const cancel = (): void => {
  resetDraft();
  dialog.value?.close();
};
const validateSchedule = (): boolean => {
  for (const key of Object.keys(periodErrors)) delete periodErrors[key];
  Object.assign(periodErrors, weeklyScheduleErrors(schedule));
  return Object.keys(periodErrors).length === 0;
};
const save = (): void => {
  if (!validateSchedule()) return;
  emit('update', serializeWeeklySchedule(schedule));
  dialog.value?.close();
};
const addPeriod = (day: WeekDay): void => {
  schedule[day].push({ on: '09:00', off: '17:00' });
  validateSchedule();
};
const removePeriod = (day: WeekDay, index: number): void => {
  schedule[day].splice(index, 1);
  validateSchedule();
};
const updateTime = (day: WeekDay, index: number, key: keyof SchedulePeriod, event: Event): void => {
  schedule[day][index]![key] = (event.target as HTMLInputElement).value;
  validateSchedule();
};
</script>

<style scoped>
.schedule-property {
  display: grid;
  gap: var(--space-2);
  color: var(--color-text-secondary);
  font-size: var(--font-size-xs);
}
.schedule-property p,
header p,
.empty-day {
  margin: 0;
  color: var(--color-text-subtle);
  font-weight: var(--font-weight-regular);
}
.schedule-property button {
  justify-self: start;
}
.schedule-dialog {
  width: min(900px, calc(100vw - 2rem));
}
.weekly-schedule {
  display: grid;
  gap: var(--space-6);
  min-width: min(760px, calc(100vw - 6rem));
}
header h2 {
  margin: 0 0 var(--space-2);
}
.days {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-5);
}
.schedule-day {
  display: grid;
  align-content: start;
  gap: var(--space-2);
  padding: var(--space-4);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-sm);
}
.day-heading,
footer {
  display: flex;
  gap: var(--space-3);
  align-items: center;
  justify-content: space-between;
}
button {
  min-height: 32px;
  padding: var(--space-1-5) var(--space-3);
}
.schedule-period {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: var(--space-2);
  align-items: end;
}
.schedule-period label {
  display: grid;
  gap: var(--space-1);
  color: var(--color-text-secondary);
  font-size: var(--font-size-xs);
  font-weight: var(--font-weight-bold);
}
.schedule-period input {
  min-width: 0;
}
.schedule-period small {
  grid-column: 1 / -1;
  color: var(--color-danger-strong);
}
.schedule-period label small {
  color: var(--color-text-subtle);
  font-weight: var(--font-weight-regular);
}
footer {
  justify-content: flex-end;
  padding-top: var(--space-2);
}
@media (max-width: 720px) {
  .weekly-schedule {
    min-width: 0;
  }
  .days {
    grid-template-columns: 1fr;
  }
}
</style>
