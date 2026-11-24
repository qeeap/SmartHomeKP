"""Проверка форм SmartHome на отдельной тестовой базе с двумя владельцами и гостем."""
import html
import re
import sys
from datetime import datetime, timedelta, timezone
from http.cookiejar import CookieJar
from urllib.error import HTTPError
from urllib.parse import parse_qs, urlencode, urlparse
from urllib.request import HTTPCookieProcessor, build_opener
from uuid import uuid4

ROOT = sys.argv[1].rstrip("/")
RUN = uuid4().hex[:12]
PASSWORD = uuid4().hex


class Session:
    def __init__(self):
        self.client = build_opener(HTTPCookieProcessor(CookieJar()))

    def fetch(self, path, data=None):
        body = urlencode(data, doseq=True).encode() if data is not None else None
        with self.client.open(ROOT + path, body, timeout=30) as response:
            self.location = response.geturl()
            return html.unescape(response.read().decode("utf-8"))

    def submit(self, page, action, **fields):
        form = self.fetch(page)
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', form)
        assert token, page
        return self.fetch(action, dict(fields, __RequestVerificationToken=token[1]))


def account(name):
    return dict(Login=name + RUN, Password=PASSWORD, FullName=name, Email=name + RUN + "@example.test")


def device(session, name):
    page = session.submit("/Devices/Create", "/Devices/Create", Name=name, Type="Light", Model="Test")
    assert name in page
    return re.search(r'/Devices/TogglePower/(\d+)', page)[1]


owner, other, guest = Session(), Session(), Session()
assert "Login" in owner.fetch("/") or "/Account/Login" in owner.location
for session, name in [(owner, "owner"), (other, "other")]:
    page = session.submit("/Account/Register", "/Account/Register", **account(name))
    assert "Мои устройства" in page

lamp = "Lamp" + RUN
lamp_id = device(owner, lamp)
second_lamp = "Second" + RUN
second_id = device(other, second_lamp)
assert lamp not in other.fetch("/Devices")
sensor_name = "Temperature" + RUN
page = owner.submit("/Sensor/Create", "/Sensor/Create", Name=sensor_name, Type="Temperature", Unit="C")
assert sensor_name in page and sensor_name not in other.fetch("/Sensor")
sensor_id = re.search(r'name="sensorId" value="(\d+)"', page)[1]
page = owner.submit("/GuestPermission/CreateGuest", "/GuestPermission/CreateGuest", **account("guest"))
guest_id = parse_qs(urlparse(owner.location).query)["guestId"][0]
future = (datetime.now() + timedelta(days=2)).strftime("%Y-%m-%dT%H:%M")
def grant():
    return owner.submit("/GuestPermission/Create", "/GuestPermission/Create",
                        guestId=guest_id, expiresAt=future, selectedDeviceIds=lamp_id)

grant()
page = grant()
grants = re.findall(r'/GuestPermission/Revoke/(\d+)', page)
assert len(grants) == 2
guest.submit("/Account/Login", "/Account/Login", Login="guest"+RUN, Password=PASSWORD)
toggle = "/Devices/TogglePower/" + lamp_id
assert lamp in guest.fetch("/Devices") and second_lamp not in guest.fetch("/Devices")
def revoke(permission):
    owner.submit("/GuestPermission", "/GuestPermission/Revoke/" + permission)
revoke(grants[0])
assert "⏻ Включено" in guest.submit("/Devices", toggle)
assert lamp in owner.fetch("/Event") and lamp not in other.fetch("/Event")
assert lamp not in owner.fetch("/Event?from=2000-01-01&to=2000-01-02")
revoke(grants[1])
assert lamp not in guest.fetch("/Devices")
try:
    guest.submit("/Devices", toggle)
except HTTPError as denied:
    assert denied.code == 404
else:
    raise AssertionError("Гость смог переключить устройство после отзыва доступа.")

assert "⏻ Выключено" in owner.submit("/Devices", toggle)
rule = "Threshold" + RUN
page = owner.submit("/Scenario/Create", "/Scenario/Create", name=rule, sensorId=sensor_id, op=">",
                    threshold="20", deviceId=lamp_id, commandType="On")
assert rule in page
for value in ["20", "21"]:
    owner.submit("/Sensor", "/Sensor/SimulateReading", sensorId=sensor_id, value=value)
    is_on = "⏻ Включено" in owner.fetch("/Devices")
    assert is_on == (value == "21")
assert rule in owner.fetch("/Notification") and rule not in other.fetch("/Notification")
assert "ScenarioEngine" in owner.fetch("/Event")
print("Проверка пройдена. Доступ владельцев и гостя, отзыв разрешений, переключение устройства, порог датчика, журнал и уведомления работают.")
