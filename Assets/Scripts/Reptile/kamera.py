import cv2
import numpy as np

# 0 = eingebaute Laptop-Kamera
CAMERA_INDEX = 0

camera = cv2.VideoCapture(CAMERA_INDEX)

if not camera.isOpened():
    print(f"Kamera {CAMERA_INDEX} konnte nicht geöffnet werden.")
    print("Schließe andere Programme, die die Kamera verwenden (z. B. Camo oder die Kamera-App).")
    raise SystemExit

print("Kamera geöffnet. Drücke Q im Videofenster zum Beenden.")

failed_reads = 0

while True:
    ok, frame = camera.read()

    if not ok:
        failed_reads += 1
        if failed_reads >= 30:
            print("Keine Bilder von der Kamera erhalten.")
            break
        continue

    failed_reads = 0

    # In HSV umwandeln, um Orange anhand der Farbe zu erkennen
    hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)

    # Orange-Bereich; Werte bei Bedarf anpassen
    lower_orange = np.array([5, 100, 100])
    upper_orange = np.array([25, 255, 255])
    mask = cv2.inRange(hsv, lower_orange, upper_orange)

    # Orange Flächen finden
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    for contour in contours:
        if cv2.contourArea(contour) > 150:
            x, y, w, h = cv2.boundingRect(contour)
            center_x = x + w // 2
            center_y = y + h // 2

            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 0), 2)
            cv2.circle(frame, (center_x, center_y), 5, (255, 0, 0), -1)
            cv2.putText(frame, "Orange erkannt", (x, max(y - 10, 20)),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)

    cv2.imshow("Laptop-Kamera - Orange erkennen", frame)

    if cv2.waitKey(1) & 0xFF == ord("q"):
        break

camera.release()
cv2.destroyAllWindows()
