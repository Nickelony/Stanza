import { randomUUID } from 'node:crypto';
import { EventEmitter } from 'node:events';
import type { Logger } from './logger.ts';
import { Order, type OrderLine } from './order.ts';
import { assertPositive } from './guards.ts';
const MAX_LINES = 50;
const RETRIES = 3;
let counter = 0;
export type OrderId = string;
export type Total = number;
export class OrderService extends EventEmitter {
	private readonly lines: OrderLine[] = [];
	private readonly logger: Logger;
	static instances = 0;
	static label = 'orders';
	#secret = randomUUID();
	@Memoize()
	public cache = new Map<OrderId, Order>();
	public size = 0;
	constructor(logger: Logger) {
		super();
		this.logger = logger;
		OrderService.instances += 1;
	}
	get count(): number {
		return this.lines.length;
	}
	set count(next: number) {
		assertPositive(next);
		this.size = next;
	}
	find(id: OrderId): Order | undefined;
	find(id: OrderId, strict: true): Order;
	find(id: OrderId, strict?: boolean): Order | undefined {
		const found = this.cache.get(id);
		if (found === undefined && strict) {
			throw new Error(`Order ${id} is missing.`);
		}
		return found;
	}
	async place(lines: OrderLine[]): Promise<Order> {
		if (lines.length === 0) return new Order([]);
		if (lines.length > MAX_LINES) {
			throw new RangeError('Too many lines.');
		}
		const order = new Order(lines);
		const total = order.total();
		this.logger.info('Placing', order.id);
		this.logger.info('Total', total);
		await this.persist(order);
		await this.notify(order);
		this.cache.set(order.id, order);
		counter += 1;
		return order;
	}
	private async persist(order: Order): Promise<void> {
		for (let attempt = 0; attempt < RETRIES; attempt += 1) {
			try {
				await save(order);
				return;
			} catch (error) {
				if (attempt === RETRIES - 1) throw error;
				this.logger.warn('Retrying', attempt);
			}
		}
	}
	private notify(order: Order): Promise<void> {
		// Tell the listeners.
		this.emit('placed', order);
		return Promise.resolve();
	}
	static reset(): void {
		OrderService.instances = 0;
		counter = 0;
	}
	static {
		OrderService.label = 'orders';
		OrderService.instances = 0;
		counter = 0;
	}
}
declare function save(order: Order): Promise<void>;
export function describeTotal(total: Total): string {
	switch (true) {
		case total > 1000:
			return 'large';
		case total > 100:
		case total > 50:
			return 'medium';
		default:
			return 'small';
	}
}
