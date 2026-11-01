import './setup.ts';
import assert from 'node:assert/strict';
import { beforeEach, describe, it } from 'node:test';

import { createLogger } from './logger.ts';
import { OrderService } from './order-service.ts';

let service: OrderService;
const lines = [{ sku: 'a', quantity: 1 }];
describe('OrderService', () => {
	beforeEach(() => {
		service = new OrderService(createLogger());
		OrderService.reset();
	});
	it('counts the lines', async () => {
		const order = await service.place(lines);
		const total = order.total();
		expect(total).toBe(1);
		expect(order.lines).toHaveLength(1);
		expect(service.count).toBe(1);
		cleanup();
	});
	it('rejects an empty order', async () => {
		await expect(service.place([])).rejects.toThrow();
		await expect(service.place([])).rejects.toThrow('empty');
		assert.equal(service.count, 0);
		assert.ok(service);
	});
	it.todo('retries');
	it.todo('notifies');
	describe('find', () => {
		it('finds an order', async () => {
			const order = await service.place(lines);
			const found = service.find(order.id);
			assert.equal(found, order);
		});
		it('throws when strict', () => {
			assert.throws(() => service.find('x', true));
		});
	});
});
function cleanup(): void {
	service.removeAllListeners();
	OrderService.reset();
}
