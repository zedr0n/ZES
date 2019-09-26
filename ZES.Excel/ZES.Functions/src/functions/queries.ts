import { request } from 'graphql-request';

export function graphQlQuery(server : string, 
                                     query : string, 
                                     parseFn: ( data : any ) => string,
                                     period : number,
                                     invocation : CustomFunctions.StreamingInvocation<string>) : void {
    const timer = setInterval(() => {
        invocation.setResult("Querying...");
        request(server, query).then(data =>
            invocation.setResult(parseFn(data)))
    }, period);

    invocation.onCanceled = () => {
        clearInterval(timer);
    };
}
