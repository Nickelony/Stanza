'use client';

import { Button } from './Button.tsx';
import { useCallback, useEffect, useState } from 'react';

import { fetchOrders } from './api.ts';
import './OrderList.css';

interface Props {
	userId: string;
	onSelect(id: string): void;
	onClose(): void;
	title?: string;
	empty?: string;
}
export function OrderList({ userId, onSelect, title = 'Orders', empty = 'None' }: Props) {
	const [orders, setOrders] = useState<string[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<Error | undefined>();
	useEffect(() => {
		let active = true;
		fetchOrders(userId).then((next) => {
			if (!active) return;
			setOrders(next);
			setLoading(false);
		});
		return () => {
			active = false;
		};
	}, [userId]);
	const handleClick = useCallback((id: string) => onSelect(id), [onSelect]);
	if (loading) return <p>Loading...</p>;
	if (error) {
		return <p role="alert">{error.message}</p>;
	}
	return (
		<section>
			{/* The heading. */}
			<h2>{title}</h2>
			<p>
				You have <b>{orders.length}</b> orders, <i>newest</i> first.
			</p>
			{orders.length === 0 ? <p>{empty}</p> : null}
			<Badge />

			<Badge />
			{/* Another one. */}
			<Badge />
			<ul>
				{orders.map((id) => (
					<li key={id}>
						<Button onClick={() => handleClick(id)}>Open {id}</Button>
					</li>
				))}
			</ul>
			<footer>
				<Button onClick={() => setError(new Error('x'))}>Fail</Button>
			</footer>
		</section>
	);
}
