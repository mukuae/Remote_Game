import cv2
import numpy as np

CAMERA_INDEX = 1

camera = cv2.VideoCapture(CAMERA_INDEX, cv2.CAP_DSHOW)


if not camera.isOpened():
    print(f"Could not open camera {CAMERA_INDEX}.")
    print("Keep Camo Studio open and check that your phone is selected.")
    raise SystemExit

print("Camera opened. Press Q in the video window to quit.")

failed_reads = 0

while True:
    ok, frame = camera.read()

    if not ok:
        failed_reads += 1
        if failed_reads >= 30:
            print("No video frames received. Check the Camo Studio preview.")
            break
        continue

    failed_reads = 0

    # Convert the image to HSV so we can detect orange by color
    hsv = cv2.cvtColor(frame, cv2.COLOR_BGR2HSV)

    # Orange color range; adjust these values if needed
    lower_orange = np.array([5, 100, 100])
    upper_orange = np.array([25, 255, 255])
    mask = cv2.inRange(hsv, lower_orange, upper_orange)

    # Find orange shapes in the image
    contours, _ = cv2.findContours(
        mask,
        cv2.RETR_EXTERNAL,
        cv2.CHAIN_APPROX_SIMPLE
    )

    for contour in contours:
        if cv2.contourArea(contour) > 150:
            x, y, w, h = cv2.boundingRect(contour)
            center_x = x + w // 2
            center_y = y + h // 2

            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 0), 2)
            cv2.circle(frame, (center_x, center_y), 5, (255, 0, 0), -1)
            cv2.putText(
                frame,
                "Orange detected",
                (x, max(y - 10, 20)),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.6,
                (0, 255, 0),
                2
            )

    cv2.imshow("Phone camera - orange detection", frame)

    if cv2.waitKey(1) & 0xFF == ord("q"):
        break

camera.release()
cv2.destroyAllWindows()
