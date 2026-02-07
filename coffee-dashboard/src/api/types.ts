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
