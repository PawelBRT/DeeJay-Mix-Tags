# Eksport bazy DJOID do JSON

1. Otworz DJOID w przegladarce.
2. Otworz narzedzia deweloperskie (`Ctrl+Shift+I`) i przejdz do zakladki `Console`.
3. Wklej ponizszy skrypt i zatwierdz Enterem.
4. Przegladarka pobierze plik `djoid-localforage3.json`.

```javascript
(async () => {
  const dbName = 'localforage';
  const storeName = 'keyvaluepairs';

  const req = indexedDB.open(dbName);
  const db = await new Promise((resolve, reject) => {
    req.onerror = () => reject(req.error);
    req.onsuccess = () => resolve(req.result);
  });

  const tx = db.transaction(storeName, 'readonly');
  const store = tx.objectStore(storeName);

  const all = [];
  await new Promise((resolve) => {
    const cursorReq = store.openCursor();
    cursorReq.onerror = () => resolve();
    cursorReq.onsuccess = e => {
      const cursor = e.target.result;
      if (cursor) {
        all.push({ key: cursor.key, value: cursor.value });
        cursor.continue();
      } else {
        resolve();
      }
    };
  });

  const blob = new Blob([JSON.stringify(all, null, 2)], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'djoid-localforage3.json';
  a.click();
  URL.revokeObjectURL(url);

  console.log('wyeksportowano', all.length, 'rekordow');
})();
```

Ten eksport zawiera cala zawartosc localforage DJOID. Aplikacja korzysta z wpisu `DJSAPP` i sekcji `tracks.tracksByIds`.
