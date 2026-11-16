export async function process(db, object) {
 const [cart, item] = loadCart();
 const limit = 10;
 db.add(cart);
 await db.flush();
 await db.refresh(cart);
 object.name = cart.name;
 object.count += 1;
 object.child.name = item.name;
 object.child.save();
 cache.clear();
 db.save();
 self.db.update(cart);
 self.db.save();
 return cart;
}

class Service {
 #db;
 async process(cart) {
  this.#db.add(cart);
  await this.#db.flush();
  this.#db.child.name = cart.name;
  this.#db.child.save();
  this.cache.clear();
  return cart;
 }
}
