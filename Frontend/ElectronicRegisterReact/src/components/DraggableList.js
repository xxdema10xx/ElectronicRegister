// src/components/DraggableList.js
// Lista verticale riordinabile trascinando la maniglia di ogni riga (mouse su web, dito su nativo).
// Usa solo PanResponder: le righe restano al loro posto nel layout e durante il trascinamento
// si spostano con un transform; al rilascio si chiama onReorder(from, to).
import { useRef, useState } from "react";
import { PanResponder, Platform, View } from "react-native";

function Row({ itemKey, handlers, style, onLayout, children }) {
  const responder = useRef(PanResponder.create({
    onStartShouldSetPanResponder: () => true,
    onMoveShouldSetPanResponder: () => true,
    onPanResponderTerminationRequest: () => false, // la ScrollView non deve "rubare" il gesto
    onPanResponderGrant: () => handlers.current.start(itemKey),
    onPanResponderMove: (_, g) => handlers.current.move(g.dy),
    onPanResponderRelease: (_, g) => handlers.current.end(g.dy),
    onPanResponderTerminate: () => handlers.current.cancel(),
  })).current;
  return <View onLayout={onLayout} style={style}>{children(responder.panHandlers)}</View>;
}

export default function DraggableList({ data, keyExtractor, renderItem, onReorder, gap = 6 }) {
  const layouts = useRef({}); // chiave -> { y, height }
  const dragRef = useRef(null); // { key, from }
  const [drag, setDrag] = useState(null); // { key, from, to, dy }
  const latest = useRef();
  latest.current = { data, keyExtractor, onReorder };

  // Posizione di arrivo: quante altre righe hanno il centro sopra il centro della riga trascinata.
  function targetIndex(from, dy) {
    const { data, keyExtractor } = latest.current;
    const l = layouts.current[keyExtractor(data[from])];
    if (!l) return from;
    const center = l.y + l.height / 2 + dy;
    return data.reduce((to, item, i) => {
      const o = layouts.current[keyExtractor(item)];
      return i !== from && o && o.y + o.height / 2 < center ? to + 1 : to;
    }, 0);
  }

  const handlers = useRef();
  handlers.current = {
    start(key) {
      const from = latest.current.data.findIndex(item => latest.current.keyExtractor(item) === key);
      dragRef.current = { key, from };
      setDrag({ key, from, to: from, dy: 0 });
    },
    move(dy) {
      const d = dragRef.current;
      if (d) setDrag({ ...d, dy, to: targetIndex(d.from, dy) });
    },
    end(dy) {
      const d = dragRef.current;
      dragRef.current = null;
      setDrag(null);
      if (!d) return;
      const to = targetIndex(d.from, dy);
      if (to !== d.from) latest.current.onReorder(d.from, to);
    },
    cancel() { dragRef.current = null; setDrag(null); },
  };

  // Spostamento delle altre righe per fare spazio a quella trascinata
  function shiftOf(i) {
    if (!drag || i === drag.from) return 0;
    const h = (layouts.current[drag.key]?.height ?? 0) + gap;
    if (drag.from < drag.to && i > drag.from && i <= drag.to) return -h;
    if (drag.to < drag.from && i >= drag.to && i < drag.from) return h;
    return 0;
  }

  return (
    <View style={[{ gap }, drag && Platform.OS === "web" && { userSelect: "none" }]}>
      {data.map((item, index) => {
        const key = keyExtractor(item);
        const dragging = drag?.key === key;
        return (
          <Row key={key} itemKey={key} handlers={handlers}
            onLayout={(e) => { layouts.current[key] = e.nativeEvent.layout; }}
            style={dragging
              ? { zIndex: 10, transform: [{ translateY: drag.dy }], shadowColor: "#000", shadowOpacity: 0.18, shadowRadius: 10, shadowOffset: { width: 0, height: 4 }, elevation: 6 }
              : { transform: [{ translateY: shiftOf(index) }] }}>
            {(dragHandleProps) => renderItem({ item, index, dragHandleProps, dragging })}
          </Row>
        );
      })}
    </View>
  );
}
