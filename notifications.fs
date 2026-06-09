namespace Notifications

// Struttura della notifica visiva classica
type NotificationPayload = {
    title: string
    body: string
}

// Configurazione specifica per il comportamento su Android
type AndroidConfig = {
    priority: string // "high" o "normal"
}

// Struttura radice del messaggio richiesta da Firebase HTTP v1
type MessageDetails = {
    token: string
    notification: NotificationPayload
    android: AndroidConfig
}

type FcmPayload = {
    message: MessageDetails
}
