// Mirrors CoffeeApi DTOs

export interface SnapshotResponse {
  id: number;
  timestamp: string;
  totalBeverages: number;
  beverageCounterCoffee: number;
  beverageCounterCoffeeAndMilk: number;
  beverageCounterMilk: number;
  beverageCounterHotWaterCups: number;
  beverageCounterHotWater: number;
  operationState: string;
}

export interface Pagination {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface PaginatedResponse<T> {
  data: T[];
  pagination: Pagination;
}

export interface DailySummary {
  coffeeToday: number;
  milkDrinksToday: number;
  totalToday: number;
  peakHour: number | null;
}

export interface DailyStatsResponse {
  date: string;
  snapshots: SnapshotResponse[];
  summary: DailySummary;
}

export interface DailyAggregate {
  date: string;
  coffeeCount: number;
  milkCount: number;
  total: number;
}

export interface RangeStatsResponse {
  from: string;
  to: string;
  data: DailyAggregate[];
}

export interface HeatmapDataPoint {
  dayOfWeek: number; // ISO-8601: 1=Monday, 7=Sunday
  hour: number;
  count: number;
}

export interface HeatmapResponse {
  weeks: number;
  heatmap: HeatmapDataPoint[];
}

export interface HealthResponse {
  status: string;
  timestamp: string;
  database: string;
  lastSnapshot: string | null;
}

export type MarkedDayKind = 'mass-import' | 'event';

export type EventType =
  | 'birthday'
  | 'visitors'
  | 'party'
  | 'sick'
  | 'vacation'
  | 'other';

export interface MarkedDay {
  date: string;            // yyyy-MM-dd
  kind: MarkedDayKind;
  eventType: EventType | null;
  reason: string;
  createdAt: string;       // ISO timestamp
}

export interface CreateMarkedDayPayload {
  date: string;            // yyyy-MM-dd
  kind: MarkedDayKind;
  eventType?: EventType;
  reason: string;
}

export interface CoffeeStatus {
  status: 'ok' | 'error';
  reachable: boolean;
  powerState: 'on' | 'off' | 'standby' | null;
  operationState: 'inactive' | 'ready' | 'run' | 'pause' | 'finished' | 'error' | null;
  label: string;
  lastUpdated: string;
  message?: string;
}
