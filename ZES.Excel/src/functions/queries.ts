import { request } from 'graphql-request';

export function graphQlQuery(server : string, 
                                     query : string, 
                                     parseFn: ( data : any ) => string,
                                     period : number,
                                     invocation : CustomFunctions.StreamingInvocation<string>) : void {
    const timer = setInterval(() => {
        invocation.setResult("Querying...");
        try {
            request(server, query).then(data =>
                invocation.setResult(parseFn(data)))
                .catch(r => invocation.setResult(r.message))
        }
        catch(error) {
            invocation.setResult(error.message)
        }
    }, period);

    invocation.onCanceled = () => {
        clearInterval(timer);
    };
}
