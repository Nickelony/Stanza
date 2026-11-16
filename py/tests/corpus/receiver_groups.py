"""Direct calls, writes, paths, qualifiers and declaration shapes."""


async def work(self, cart):
    prepare()
    db.add(cart)
    db.add(cart)

    await db.flush()
    db.child.name = cart
    db.child.save()
    cache.clear()
    db.child.save()
    self.db.add(cart)
    self.db.child.name = cart
    self.db.child.save()
    self.self.db.add(cart)
    self.self.cache.clear()
    first, second = read_pair()
    third, fourth = read_pair()
    count = 1
    label = "ready"
    finish()
